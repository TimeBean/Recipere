namespace Recipere.Core.Model;

public sealed class Text
{
    public string Value { get; }

    public Text(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public static implicit operator string(Text text) => text.Value;
}