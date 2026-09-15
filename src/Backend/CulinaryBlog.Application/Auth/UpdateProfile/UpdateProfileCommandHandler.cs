using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Auth.Mappers;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Auth.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateProfileCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        user.UpdateProfile(request.FullName, request.AvatarUrl);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new FluentValidation.ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.FullName), e.Description)));
        }

        var roles = await _userManager.GetRolesAsync(user);

        return UserProfileMapper.ToDto(user, roles);
    }
}
