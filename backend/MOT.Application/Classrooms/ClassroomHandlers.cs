using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Classrooms
{
    public class ClassroomHandlers : 
        IRequestHandler<JoinRoomCommand, RoomParticipantDto>,
        IRequestHandler<PingRoomCommand, Unit>,
        IRequestHandler<LeaveRoomCommand, Unit>,
        IRequestHandler<GetRoomParticipantsQuery, List<RoomParticipantDto>>
    {
        private const int MaxClassroomSeats = 50;
        private const int MaxJoinRetries = 3;

        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ClassroomHandlers(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<RoomParticipantDto> Handle(JoinRoomCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            for (int attempt = 0; attempt < MaxJoinRetries; attempt++)
            {
                // 1. Purge stale participants (>60s inactivity) in this classroom
                    var staleThreshold = DateTime.UtcNow.AddSeconds(-60);
                    var staleParticipants = await _context.RoomParticipants
                        .Where(p => p.RoomName == request.RoomName && p.LastPing < staleThreshold)
                        .ToListAsync(cancellationToken);

                    if (staleParticipants.Count > 0)
                    {
                        _context.RoomParticipants.RemoveRange(staleParticipants);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    // 2. Check if user already occupies a seat in this room (idempotent re-join)
                    var existing = await _context.RoomParticipants
                        .FirstOrDefaultAsync(p => p.RoomName == request.RoomName && p.UserId == userId, cancellationToken);

                    if (existing != null)
                    {
                        existing.LastPing = DateTime.UtcNow;
                        existing.IsVideoOn = request.IsVideoOn;
                        await _context.SaveChangesAsync(cancellationToken);

                        return new RoomParticipantDto
                        {
                            UserId = userId,
                            Username = existing.Username,
                            SeatNumber = existing.SeatNumber,
                            IsVideoOn = existing.IsVideoOn
                        };
                    }

                    // 3. Find taken seats and assign the lowest free seat in 0..49
                    var takenSeats = await _context.RoomParticipants
                        .Where(p => p.RoomName == request.RoomName)
                        .Select(p => p.SeatNumber)
                        .ToListAsync(cancellationToken);

                    int assignedSeat = -1;
                    for (int s = 0; s < MaxClassroomSeats; s++)
                    {
                        if (!takenSeats.Contains(s))
                        {
                            assignedSeat = s;
                            break;
                        }
                    }

                    if (assignedSeat == -1)
                    {
                        throw new ClassroomFullException(request.RoomName);
                    }

                    // 4. Create and persist new participant
                    var participant = new RoomParticipant
                    {
                        Id = Guid.NewGuid(),
                        RoomName = request.RoomName,
                        UserId = userId,
                        Username = user?.DisplayName ?? "Anonymous",
                        SeatNumber = assignedSeat,
                        IsVideoOn = request.IsVideoOn,
                        JoinedAt = DateTime.UtcNow,
                        LastPing = DateTime.UtcNow
                    };

                    _context.RoomParticipants.Add(participant);
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);

                        return new RoomParticipantDto
                        {
                            UserId = userId,
                            Username = participant.Username,
                            SeatNumber = participant.SeatNumber,
                            IsVideoOn = participant.IsVideoOn
                        };
                    }
                    catch (DbUpdateException ex)
                    {
                        _context.RoomParticipants.Remove(participant);

                        if (IsSeatOrUserCollision(ex) && attempt < MaxJoinRetries - 1)
                        {
                            continue;
                        }

                        if (IsSeatOrUserCollision(ex))
                        {
                            throw new ClassroomSeatConflictException(request.RoomName, -1);
                        }

                        throw;
                    }
                }

            throw new ClassroomSeatConflictException(request.RoomName, -1);
        }

        private static bool IsSeatOrUserCollision(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("1062") ||
                   msg.Contains("UQ_Room", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("SeatNumber", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("UserId", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<Unit> Handle(PingRoomCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException();

            var existing = await _context.RoomParticipants
                .FirstOrDefaultAsync(p => p.RoomName == request.RoomName && p.UserId == userId, cancellationToken);

            if (existing != null)
            {
                existing.LastPing = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Unit.Value;
        }

        public async Task<Unit> Handle(LeaveRoomCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException();

            // Strict ownership isolation: only the authenticated user can release their own seat
            var existing = await _context.RoomParticipants
                .FirstOrDefaultAsync(p => p.RoomName == request.RoomName && p.UserId == userId, cancellationToken);

            if (existing != null)
            {
                _context.RoomParticipants.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Unit.Value;
        }

        public async Task<List<RoomParticipantDto>> Handle(GetRoomParticipantsQuery request, CancellationToken cancellationToken)
        {
            var staleThreshold = DateTime.UtcNow.AddSeconds(-60);

            // Clean up stale participants so returned seat list is strictly accurate
            var staleParticipants = await _context.RoomParticipants
                .Where(p => p.RoomName == request.RoomName && p.LastPing < staleThreshold)
                .ToListAsync(cancellationToken);

            if (staleParticipants.Count > 0)
            {
                _context.RoomParticipants.RemoveRange(staleParticipants);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return await _context.RoomParticipants
                .Where(p => p.RoomName == request.RoomName && p.LastPing >= staleThreshold)
                .OrderBy(p => p.SeatNumber)
                .Select(p => new RoomParticipantDto
                {
                    UserId = p.UserId,
                    Username = p.Username,
                    SeatNumber = p.SeatNumber,
                    IsVideoOn = p.IsVideoOn
                })
                .ToListAsync(cancellationToken);
        }
    }
}
