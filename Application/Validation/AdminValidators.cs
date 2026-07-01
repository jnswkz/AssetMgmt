using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Departments;
using AssetMgmt.Application.Requests;
using AssetMgmt.Application.Users;
using FluentValidation;

namespace AssetMgmt.Application.Validation;

// ---------- Auth ----------

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ---------- Requests ----------

public class CreateRequestDtoValidator : AbstractValidator<CreateRequestDto>
{
    public CreateRequestDtoValidator()
    {
        RuleFor(x => x.AssetInstanceId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.ExpectedDurationMonths)
            .InclusiveBetween(1, 120).When(x => x.ExpectedDurationMonths.HasValue);
    }
}

public class RejectRequestDtoValidator : AbstractValidator<RejectRequestDto>
{
    public RejectRequestDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

// ---------- Users ----------

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

// ---------- Departments ----------

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class AssignManagerRequestValidator : AbstractValidator<AssignManagerRequest>
{
    public AssignManagerRequestValidator()
    {
        RuleFor(x => x.ManagerId).NotEmpty();
    }
}
