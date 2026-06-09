using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;

namespace StuRoom.Controllers;

public class LanguageController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        // Set cookie for ASP.NET Request Localization middleware
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );

        // Redirect back to page where user changed language
        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
