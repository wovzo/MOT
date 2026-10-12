using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Classrooms;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Classrooms;

public class ClassroomHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<RoomParticipant> _participants;
    private readonly List<User> _users;

    public ClassroomHandlerTests()
    {
        _participants = new List<RoomParticipant>();
        _users = new List<User>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var mockParticipantDbSet = MockDbSetHelper.CreateMockDbSet(_participants);
        var mockUserDbSet = MockDbSetHelper.CreateMockDbSet(_users);

        _dbContextMock.Setup(c => c.RoomParticipants).Returns(mockParticipantDbSet.Object);
        _dbContextMock.Setup(c => c.Users).Returns(mockUserDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task JoinRoom_WhenValid_AssignsLowestAvailableSeat()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Alice" });

        // Seat 0 and 1 are already taken in room 'study-101'
        _participants.Add(new RoomParticipant { Id = Guid.NewGuid(), RoomName = "study-101", UserId = Guid.NewGuid(), SeatNumber = 0, LastPing = DateTime.UtcNow });
        _participants.Add(new RoomParticipant { Id = Guid.NewGuid(), RoomName = "study-101", UserId = Guid.NewGuid(), SeatNumber = 1, LastPing = DateTime.UtcNow });

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "study-101", IsVideoOn = false };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.SeatNumber.Should().Be(2); // Lowest available seat
        result.UserId.Should().Be(userId);
        result.Username.Should().Be("Alice");
        result.IsVideoOn.Should().BeFalse();
        _participants.Should().HaveCount(3);
    }

    [Fact]
    public async Task JoinRoom_WhenRejoining_UpdatesIsVideoOnAndKeepsSameSeat_Idempotent()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Bob" });

        var existing = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "study-101",
            UserId = userId,
            Username = "Bob",
            SeatNumber = 5,
            IsVideoOn = false,
            JoinedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddSeconds(-10)
        };
        _participants.Add(existing);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "study-101", IsVideoOn = true };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.SeatNumber.Should().Be(5);
        result.IsVideoOn.Should().BeTrue();
        existing.IsVideoOn.Should().BeTrue();
        _participants.Should().HaveCount(1); // No duplicate participant row
    }

    [Fact]
    public async Task JoinRoom_WhenRoomAtCapacity50_ThrowsClassroomFullException()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "User51" });

        // Fill all 50 seats (0 through 49)
        for (int i = 0; i < 50; i++)
        {
            _participants.Add(new RoomParticipant
            {
                Id = Guid.NewGuid(),
                RoomName = "full-room",
                UserId = Guid.NewGuid(),
                SeatNumber = i,
                LastPing = DateTime.UtcNow
            });
        }

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "full-room", IsVideoOn = false };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ClassroomFullException>()
            .WithMessage("Classroom 'full-room' is full. Maximum capacity is 50 participants.");
        _participants.Should().HaveCount(50);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task JoinRoom_WhenUnauthenticatedOrInvalidUserId_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "room-1", IsVideoOn = false };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task JoinRoom_PurgesStaleParticipantsOlderThan60Seconds_ReclaimingSeat()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Charlie" });

        // Stale participant in seat 0 (>60s inactivity)
        var stale = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "room-cleanup",
            UserId = Guid.NewGuid(),
            SeatNumber = 0,
            LastPing = DateTime.UtcNow.AddSeconds(-90)
        };
        _participants.Add(stale);

        // Active participant in seat 1
        var active = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "room-cleanup",
            UserId = Guid.NewGuid(),
            SeatNumber = 1,
            LastPing = DateTime.UtcNow
        };
        _participants.Add(active);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "room-cleanup", IsVideoOn = false };

        var result = await handler.Handle(command, CancellationToken.None);

        result.SeatNumber.Should().Be(0); // Seat 0 reclaimed from stale participant
        _participants.Should().NotContain(stale);
        _participants.Should().Contain(active);
    }

    [Fact]
    public async Task JoinRoom_WhenConcurrentSeatCollisionOccurs_RetriesAndClaimsNextAvailableSeat()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "RetryUser" });

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "busy-room", IsVideoOn = false };

        var callCount = 0;
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Simulate unique constraint race condition on first insert:
                    // Another transaction took seat 0 right before us
                    _participants.Add(new RoomParticipant
                    {
                        Id = Guid.NewGuid(),
                        RoomName = "busy-room",
                        UserId = Guid.NewGuid(),
                        SeatNumber = 0,
                        LastPing = DateTime.UtcNow
                    });
                    throw new DbUpdateException("Duplicate entry 'busy-room-0' for key 'UQ_Room_Seat'", new Exception("1062"));
                }
                return Task.FromResult(1);
            });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.SeatNumber.Should().Be(1); // Automatically retried and claimed seat 1
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task JoinRoom_WhenConcurrentDuplicateUserJoinOccurs_RetriesAndReturnsExistingSeat()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "DoubleJoinUser" });

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new JoinRoomCommand { RoomName = "race-room", IsVideoOn = true };

        var callCount = 0;
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Simulate parallel join request for same user inserted first
                    _participants.Add(new RoomParticipant
                    {
                        Id = Guid.NewGuid(),
                        RoomName = "race-room",
                        UserId = userId,
                        Username = "DoubleJoinUser",
                        SeatNumber = 3,
                        IsVideoOn = false,
                        LastPing = DateTime.UtcNow
                    });
                    throw new DbUpdateException("Duplicate entry for key 'UQ_Room_User'", new Exception("1062"));
                }
                return Task.FromResult(1);
            });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.SeatNumber.Should().Be(3);
        result.IsVideoOn.Should().BeTrue();
    }

    [Fact]
    public async Task PingRoom_WhenParticipantExists_UpdatesLastPing()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var initialTime = DateTime.UtcNow.AddMinutes(-2);
        var participant = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "ping-room",
            UserId = userId,
            SeatNumber = 0,
            LastPing = initialTime
        };
        _participants.Add(participant);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new PingRoomCommand { RoomName = "ping-room" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        participant.LastPing.Should().BeAfter(initialTime);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PingRoom_WhenParticipantNotFound_SucceedsGracefully()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new PingRoomCommand { RoomName = "empty-room" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PingRoom_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new PingRoomCommand { RoomName = "any-room" };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LeaveRoom_WhenParticipantExists_RemovesParticipant()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var participant = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "leave-room",
            UserId = userId,
            SeatNumber = 4,
            LastPing = DateTime.UtcNow
        };
        _participants.Add(participant);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new LeaveRoomCommand { RoomName = "leave-room" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        _participants.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveRoom_WhenParticipantNotFound_SucceedsGracefully()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new LeaveRoomCommand { RoomName = "already-left-room" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveRoom_OnlyReleasesOwnSeat_CannotReleaseAnotherUsersSeat()
    {
        var attackerId = Guid.NewGuid();
        var victimId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(attackerId.ToString());

        var victimParticipant = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "shared-room",
            UserId = victimId,
            SeatNumber = 10,
            LastPing = DateTime.UtcNow
        };
        _participants.Add(victimParticipant);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new LeaveRoomCommand { RoomName = "shared-room" };

        await handler.Handle(command, CancellationToken.None);

        // Victim's seat is NOT released
        _participants.Should().Contain(victimParticipant);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveRoom_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new LeaveRoomCommand { RoomName = "any-room" };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetRoomParticipants_ExcludesStaleParticipantsOlderThan60Seconds()
    {
        var activeParticipant = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "query-room",
            UserId = Guid.NewGuid(),
            Username = "ActiveUser",
            SeatNumber = 2,
            LastPing = DateTime.UtcNow
        };
        var staleParticipant = new RoomParticipant
        {
            Id = Guid.NewGuid(),
            RoomName = "query-room",
            UserId = Guid.NewGuid(),
            Username = "StaleUser",
            SeatNumber = 0,
            LastPing = DateTime.UtcNow.AddSeconds(-90)
        };
        _participants.Add(activeParticipant);
        _participants.Add(staleParticipant);

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var query = new GetRoomParticipantsQuery { RoomName = "query-room" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Username.Should().Be("ActiveUser");
        _participants.Should().NotContain(staleParticipant);
    }

    [Fact]
    public async Task GetRoomParticipants_ReturnsActiveParticipantsOrderedBySeat()
    {
        _participants.Add(new RoomParticipant { Id = Guid.NewGuid(), RoomName = "order-room", UserId = Guid.NewGuid(), SeatNumber = 7, LastPing = DateTime.UtcNow });
        _participants.Add(new RoomParticipant { Id = Guid.NewGuid(), RoomName = "order-room", UserId = Guid.NewGuid(), SeatNumber = 2, LastPing = DateTime.UtcNow });
        _participants.Add(new RoomParticipant { Id = Guid.NewGuid(), RoomName = "order-room", UserId = Guid.NewGuid(), SeatNumber = 5, LastPing = DateTime.UtcNow });

        var handler = new ClassroomHandlers(_dbContextMock.Object, _currentUserServiceMock.Object);
        var query = new GetRoomParticipantsQuery { RoomName = "order-room" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Select(p => p.SeatNumber).Should().ContainInOrder(2, 5, 7);
    }

    // Validation Tests
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("room with spaces", false)]
    [InlineData("room$special#", false)]
    [InlineData("valid-room_101", true)]
    [InlineData("MathRoom", true)]
    public void ClassroomValidators_JoinRoom_ValidatesRoomNameCorrectly(string roomName, bool isValid)
    {
        var validator = new JoinRoomCommandValidator();
        var command = new JoinRoomCommand { RoomName = roomName, IsVideoOn = false };
        var result = validator.Validate(command);
        result.IsValid.Should().Be(isValid);
    }

    // Controller Status Code Mapping Tests
    [Fact]
    public async Task ClassroomsController_Join_WhenClassroomFull_ReturnsConflict409()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<JoinRoomCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClassroomFullException("math-101"));

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Join("math-101", new JoinRequest { IsVideoOn = false });

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ClassroomsController_Join_WhenSeatConflict_ReturnsConflict409()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<JoinRoomCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClassroomSeatConflictException("math-101", 3));

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Join("math-101", new JoinRequest { IsVideoOn = false });

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ClassroomsController_Join_WhenValidationFails_ReturnsBadRequest400()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<JoinRoomCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("RoomName", "Invalid room name") }));

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Join("bad room name", new JoinRequest { IsVideoOn = false });

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ClassroomsController_Join_WhenUnauthenticated_ReturnsUnauthorized401()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<JoinRoomCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Join("room-1", new JoinRequest { IsVideoOn = false });

        var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task ClassroomsController_Join_WhenValid_ReturnsOk200()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        var participant = new RoomParticipantDto { UserId = Guid.NewGuid(), Username = "Alice", SeatNumber = 0, IsVideoOn = false };
        mediatorMock.Setup(m => m.Send(It.IsAny<JoinRoomCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(participant);

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Join("math-101", new JoinRequest { IsVideoOn = false });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(participant);
    }

    [Fact]
    public async Task ClassroomsController_Leave_WhenValid_ReturnsOk200()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<LeaveRoomCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Leave("math-101");

        var okResult = result.Should().BeOfType<OkResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ClassroomsController_Leave_WhenValidationFails_ReturnsBadRequest400()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<LeaveRoomCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("RoomName", "Room name is required") }));

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Leave("");

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ClassroomsController_Ping_WhenValid_ReturnsOk200()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<PingRoomCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.Ping("math-101");

        var okResult = result.Should().BeOfType<OkResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ClassroomsController_GetParticipants_WhenValid_ReturnsOk200()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<ClassroomsController>>();
        var participants = new List<RoomParticipantDto>
        {
            new RoomParticipantDto { UserId = Guid.NewGuid(), Username = "Alice", SeatNumber = 0, IsVideoOn = false },
            new RoomParticipantDto { UserId = Guid.NewGuid(), Username = "Bob", SeatNumber = 1, IsVideoOn = true }
        };
        mediatorMock.Setup(m => m.Send(It.IsAny<GetRoomParticipantsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(participants);

        var controller = new ClassroomsController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetParticipants("math-101");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(participants);
    }
}
