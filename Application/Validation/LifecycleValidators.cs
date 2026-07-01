using AssetMgmt.Application.Allocations;
using AssetMgmt.Domain.Enums;
using FluentValidation;

namespace AssetMgmt.Application.Validation;

public class TransferAssetDtoValidator : AbstractValidator<TransferAssetDto>
{
    public TransferAssetDtoValidator()
    {
        RuleFor(x => x.ToUserId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class ReturnAssetDtoValidator : AbstractValidator<ReturnAssetDto>
{
    public ReturnAssetDtoValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class StartMaintenanceDtoValidator : AbstractValidator<StartMaintenanceDto>
{
    public StartMaintenanceDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Vendor).MaximumLength(200);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
    }
}

public class CompleteMaintenanceDtoValidator : AbstractValidator<CompleteMaintenanceDto>
{
    public CompleteMaintenanceDtoValidator()
    {
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class DisposeAssetDtoValidator : AbstractValidator<DisposeAssetDto>
{
    public DisposeAssetDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(1000);

        // Selling requires a buyer and a non-negative price.
        When(x => x.Type == DisposalType.Sold, () =>
        {
            RuleFor(x => x.SoldToUserId).NotNull()
                .WithMessage("A buyer is required when selling an asset.");
            RuleFor(x => x.SalePrice).NotNull().GreaterThanOrEqualTo(0)
                .WithMessage("A non-negative sale price is required when selling an asset.");
        });
    }
}
