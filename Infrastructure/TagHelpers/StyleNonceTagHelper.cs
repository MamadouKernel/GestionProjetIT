using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace GestionProjects.Infrastructure.TagHelpers
{
    [HtmlTargetElement("style")]
    public class StyleNonceTagHelper : TagHelper
    {
        [ViewContext, HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = null!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!output.Attributes.ContainsName("nonce"))
            {
                var nonce = ViewContext.HttpContext.Items["CspNonce"]?.ToString();
                if (!string.IsNullOrEmpty(nonce))
                    output.Attributes.SetAttribute("nonce", nonce);
            }
        }
    }
}
