namespace Myriad.Types;

public record MessageComponent
{
    public ComponentType Type { get; init; }
    public ButtonStyle? Style { get; init; }
    public string? Label { get; init; }
    public Emoji? Emoji { get; init; }
    public string? CustomId { get; init; }
    public string? Url { get; init; }
    public bool? Disabled { get; init; }
    public bool? Required { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? Placeholder { get; init; }
    public string? Value { get; init; }
    public MessageComponent[]? Components { get; init; }
}