namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Represents an option in the parent-unit selector dropdown. The text is indented according to depth.
/// </summary>
public sealed record ParentOptionViewModel(string Value, string Text, int Depth)
{
    /// <summary>
    /// Returns an indented display text based on the node's depth in the hierarchy.
    /// </summary>
    public string IndentedText => Depth == 0 ? Text : $"{new string('\u00A0', Depth * 4)}{Text}";
}
