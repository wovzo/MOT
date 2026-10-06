using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
            var userId = Guid.Parse(userIdString);
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

            var existing = await _context.RoomParticipants
                .FirstOrDefaultAsync(p => p.RoomName == request.RoomName && p.UserId == userId, cancellationToken);

            if (existing != null)
            {
                existing.LastPing = DateTime.UtcNow;
                existing.IsVideoOn = request.IsVideoOn;
                await _context.SaveChangesAsync(cancellationToken);
                return new RoomParticipantDto { UserId = userId, Username = existing.Username, SeatNumber = existing.SeatNumber, IsVideoOn = existing.IsVideoOn };
            }

            var staleThreshold = DateTime.UtcNow.AddSeconds(-60);
            var staleParticipants = await _context.RoomParticipants
                .Where(p => p.RoomName == request.RoomName && p.LastPing < staleThreshold)
                .ToListAsync(cancellationToken);
            _context.RoomParticipants.RemoveRange(staleParticipants);

            var activeParticipants = await _context.RoomParticipants
                .Where(p => p.RoomName == request.RoomName && p.LastPing >= staleThreshold)
                .Select(p => p.SeatNumber)
                .ToListAsync(cancellationToken);

            int seat = 0;
            while (activeParticipants.Contains(seat) && seat < 50)
            {
                seat++;
            }

            var participant = new RoomParticipant
            {
                Id = Guid.NewGuid(),
                RoomName = request.RoomName ?? "Default",
                UserId = userId,
                Username = user?.DisplayName ?? "Anonymous",
                SeatNumber = seat,
                IsVideoOn = request.IsVideoOn,
                JoinedAt = DateTime.UtcNow,
                LastPing = DateTime.UtcNow
            };

            _context.RoomParticipants.Add(participant);
            await _context.SaveChangesAsync(cancellationToken);

            return new RoomParticipantDto { UserId = userId, Username = participant.Username, SeatNumber = participant.SeatNumber, IsVideoOn = participant.IsVideoOn };
        }

        public async Task<Unit> Handle(PingRoomCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            var userId = Guid.Parse(userIdString);
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
            var userId = Guid.Parse(userIdString);
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
            return await _context.RoomParticipants
                .Where(p => p.RoomName == request.RoomName && p.LastPing >= staleThreshold)
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
