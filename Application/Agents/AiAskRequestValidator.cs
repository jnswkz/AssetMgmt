using FluentValidation;

namespace AssetMgmt.Application.Agents;

public class AiAskRequestValidator : AbstractValidator<AiAskRequest>
{
    public AiAskRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
