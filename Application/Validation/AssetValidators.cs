using AssetMgmt.Application.Assets;
using FluentValidation;

namespace AssetMgmt.Application.Validation;

public class CreateAssetModelRequestValidator : AbstractValidator<CreateAssetModelRequest>
{
    public CreateAssetModelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Manufacturer).MaximumLength(200);
        RuleFor(x => x.ModelNumber).MaximumLength(200);
        RuleFor(x => x.DefaultUsefulLifeMonths)
            .GreaterThan(0).When(x => x.DefaultUsefulLifeMonths.HasValue);
        RuleFor(x => x.DefaultDepreciationMethod)
            .IsInEnum().When(x => x.DefaultDepreciationMethod.HasValue);
    }
}

public class UpdateAssetModelRequestValidator : AbstractValidator<UpdateAssetModelRequest>
{
    public UpdateAssetModelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Manufacturer).MaximumLength(200);
        RuleFor(x => x.ModelNumber).MaximumLength(200);
        RuleFor(x => x.DefaultUsefulLifeMonths).GreaterThan(0);
        RuleFor(x => x.DefaultDepreciationMethod).IsInEnum();
    }
}

public class CreateAssetInstanceRequestValidator : AbstractValidator<CreateAssetInstanceRequest>
{
    public CreateAssetInstanceRequestValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.Serial).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AcquisitionDate).NotEmpty();
        RuleFor(x => x.SalvageValue)
            .GreaterThanOrEqualTo(0).When(x => x.SalvageValue.HasValue);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}

public class UpdateAssetInstanceRequestValidator : AbstractValidator<UpdateAssetInstanceRequest>
{
    public UpdateAssetInstanceRequestValidator()
    {
        RuleFor(x => x.Serial).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AcquisitionDate).NotEmpty();
        RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}
