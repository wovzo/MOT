using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Subjects.Commands;
using MOT.Application.Subjects.Queries;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Subjects;

public class SubjectHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<Subject> _subjects;

    public SubjectHandlerTests()
    {
        _subjects = new List<Subject>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var mockDbSet = MockDbSetHelper.CreateMockDbSet(_subjects);
        _dbContextMock.Setup(c => c.Subjects).Returns(mockDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task CreateSubject_WhenValid_CreatesAndReturnsSubject()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new CreateSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateSubjectCommand
        {
            Name = "  Mathematics  ",
            Description = "Calculus and Linear Algebra",
            ColorHex = "#00D9C0"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Mathematics");
        result.Description.Should().Be("Calculus and Linear Algebra");
        result.ColorHex.Should().Be("#00D9C0");
        _subjects.Should().HaveCount(1);
        _subjects[0].UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateSubject_WhenDuplicateNameForSameUser_ThrowsDuplicateSubjectNameException()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _subjects.Add(new Subject { Id = Guid.NewGuid(), UserId = userId, Name = "Physics" });

        var handler = new CreateSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateSubjectCommand { Name = "PHYSICS" };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateSubjectNameException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateSubject_WhenSameNameForDifferentUser_Succeeds()
    {
        var otherUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());
        _subjects.Add(new Subject { Id = Guid.NewGuid(), UserId = otherUserId, Name = "Physics" });

        var handler = new CreateSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateSubjectCommand { Name = "Physics" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Name.Should().Be("Physics");
        _subjects.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSubjects_ReturnsOnlyAuthenticatedUserSubjects()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());

        _subjects.Add(new Subject { Id = Guid.NewGuid(), UserId = currentUserId, Name = "Biology" });
        _subjects.Add(new Subject { Id = Guid.NewGuid(), UserId = currentUserId, Name = "Chemistry" });
        _subjects.Add(new Subject { Id = Guid.NewGuid(), UserId = otherUserId, Name = "History" });

        var handler = new GetSubjectsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var results = await handler.Handle(new GetSubjectsQuery(), CancellationToken.None);

        results.Should().HaveCount(2);
        results.Select(s => s.Name).Should().ContainInOrder("Biology", "Chemistry");
    }

    [Fact]
    public async Task GetSubjectById_WhenOwned_ReturnsSubject()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _subjects.Add(new Subject { Id = subjectId, UserId = userId, Name = "Economics" });

        var handler = new GetSubjectByIdQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetSubjectByIdQuery(subjectId), CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(subjectId);
        result.Name.Should().Be("Economics");
    }

    [Fact]
    public async Task GetSubjectById_WhenBelongsToAnotherUser_ThrowsSubjectNotFoundException()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());
        _subjects.Add(new Subject { Id = subjectId, UserId = otherUserId, Name = "Economics" });

        var handler = new GetSubjectByIdQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var act = () => handler.Handle(new GetSubjectByIdQuery(subjectId), CancellationToken.None);

        await act.Should().ThrowAsync<SubjectNotFoundException>();
    }

    [Fact]
    public async Task UpdateSubject_WhenValid_UpdatesFields()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _subjects.Add(new Subject { Id = subjectId, UserId = userId, Name = "Old Name", Description = "Old Desc", ColorHex = "#111111" });

        var handler = new UpdateSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new UpdateSubjectCommand
        {
            Id = subjectId,
            Name = "New Name",
            Description = "New Desc",
            ColorHex = "#222222"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("New Name");
        result.Description.Should().Be("New Desc");
        result.ColorHex.Should().Be("#222222");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSubject_WhenDuplicateNameWithAnotherSubject_ThrowsDuplicateSubjectNameException()
    {
        var userId = Guid.NewGuid();
        var subject1 = Guid.NewGuid();
        var subject2 = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _subjects.Add(new Subject { Id = subject1, UserId = userId, Name = "Mathematics" });
        _subjects.Add(new Subject { Id = subject2, UserId = userId, Name = "Physics" });

        var handler = new UpdateSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new UpdateSubjectCommand { Id = subject2, Name = "mathematics" };

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateSubjectNameException>();
    }

    [Fact]
    public async Task DeleteSubject_WhenOwned_RemovesSubject()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _subjects.Add(new Subject { Id = subjectId, UserId = userId, Name = "Art" });

        var handler = new DeleteSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new DeleteSubjectCommand(subjectId), CancellationToken.None);

        _subjects.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSubject_WhenNotOwned_ThrowsSubjectNotFoundException()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());
        _subjects.Add(new Subject { Id = subjectId, UserId = otherUserId, Name = "Art" });

        var handler = new DeleteSubjectCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var act = () => handler.Handle(new DeleteSubjectCommand(subjectId), CancellationToken.None);

        await act.Should().ThrowAsync<SubjectNotFoundException>();
    }

    [Fact]
    public void CreateSubjectValidator_ValidatesNameAndColorHex()
    {
        var validator = new CreateSubjectCommandValidator();

        var emptyName = validator.Validate(new CreateSubjectCommand { Name = "" });
        emptyName.IsValid.Should().BeFalse();

        var longName = validator.Validate(new CreateSubjectCommand { Name = new string('A', 101) });
        longName.IsValid.Should().BeFalse();

        var invalidHex = validator.Validate(new CreateSubjectCommand { Name = "Math", ColorHex = "not-a-color" });
        invalidHex.IsValid.Should().BeFalse();

        var validHex = validator.Validate(new CreateSubjectCommand { Name = "Math", ColorHex = "#6C5CE7" });
        validHex.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SubjectsController_ReturnsExpectedStatusCodes()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<SubjectsController>>();
        var controller = new SubjectsController(mediatorMock.Object, loggerMock.Object);

        // GetSubjects
        mediatorMock.Setup(m => m.Send(It.IsAny<GetSubjectsQuery>(), default))
            .ReturnsAsync(new List<Application.Subjects.DTOs.SubjectDto>());
        var getRes = await controller.GetSubjects();
        getRes.Should().BeOfType<OkObjectResult>();

        // CreateSubject
        var dto = new Application.Subjects.DTOs.SubjectDto { Id = Guid.NewGuid(), Name = "Art" };
        mediatorMock.Setup(m => m.Send(It.IsAny<CreateSubjectCommand>(), default))
            .ReturnsAsync(dto);
        var postRes = await controller.CreateSubject(new CreateSubjectCommand { Name = "Art" });
        postRes.Should().BeOfType<CreatedAtActionResult>();

        // DeleteSubject NotFound
        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteSubjectCommand>(), default))
            .ThrowsAsync(new SubjectNotFoundException(Guid.NewGuid()));
        var delRes = await controller.DeleteSubject(Guid.NewGuid());
        delRes.Should().BeOfType<NotFoundObjectResult>();
    }
}
