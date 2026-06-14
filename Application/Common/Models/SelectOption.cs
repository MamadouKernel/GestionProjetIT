namespace GestionProjects.Application.Common.Models;

/// <summary>
/// Option de selection neutre, sans dependance MVC/Razor.
/// </summary>
public sealed record SelectOption(string Value, string Text, bool Selected = false, bool Disabled = false);
