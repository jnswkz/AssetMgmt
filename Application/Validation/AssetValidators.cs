using AssetMgmt.Application.Assets;
using FluentValidation;
using System.Text.Json;

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
        RuleFor(x => x.Specs).Must(JsonValidation.BeObject).When(x => !string.IsNullOrWhiteSpace(x.Specs))
            .WithMessage("Specs must be a valid JSON object.");
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
        RuleFor(x => x.Specs).Must(JsonValidation.BeObject).When(x => !string.IsNullOrWhiteSpace(x.Specs))
            .WithMessage("Specs must be a valid JSON object.");
    }
}

internal static class JsonValidation
{
    public static bool BeObject(string? value)
    {
        try
        {
            using var document = JsonDocument.Parse(value!);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
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
