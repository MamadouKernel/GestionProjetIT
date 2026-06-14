using GestionProjects.Application.Common.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Web.Ui;

public static class SelectOptionExtensions
{
    public static IEnumerable<SelectListItem> ToSelectListItems(this IEnumerable<SelectOption>? options)
    {
        return options?.Select(option => new SelectListItem
        {
            Value = option.Value,
            Text = option.Text,
            Selected = option.Selected,
            Disabled = option.Disabled
        }) ?? Enumerable.Empty<SelectListItem>();
    }
}
