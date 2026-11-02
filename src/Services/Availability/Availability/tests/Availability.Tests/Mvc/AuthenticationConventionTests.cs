using System.Reflection;
using Availability.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Tests.Mvc;

/// <summary>
/// Convención del usuario: el esquema de autenticación (Bearer en /api, Cookie en el resto) se resuelve a nivel general
/// (<c>CookieAuthenticationExtensions</c>); ningún controller ni action debe declarar <c>AuthenticationSchemes</c>.
/// </summary>
public class AuthenticationConventionTests
{
    private static readonly Assembly Web = typeof(MvcControllerBase).Assembly;

    private static IEnumerable<Type> Controllers() =>
        Web.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    private static IEnumerable<MemberInfo> ControllersAndActions() =>
        Controllers().SelectMany(t => new MemberInfo[] { t }.Concat(
            t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)));

    [Fact]
    public void NoControllerOrAction_DeclaresAuthenticationSchemes()
    {
        var offenders = ControllersAndActions()
            .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Member: m, Attribute: a)))
            .Where(x => !string.IsNullOrEmpty(x.Attribute.AuthenticationSchemes))
            .Select(x => $"{x.Member.DeclaringType?.Name}.{x.Member.Name} -> {x.Attribute.AuthenticationSchemes}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ThereAreControllers_ForBothTheApiAndThePages()
    {
        Assert.Contains(Controllers(), t => t.IsDefined(typeof(ApiControllerAttribute), inherit: true));
        Assert.Contains(Controllers(), t => typeof(MvcControllerBase).IsAssignableFrom(t));
    }

    [Fact]
    public void EveryPageController_InheritsTheMvcBase_WithAutomaticAntiforgery_AndHiddenFromSwagger()
    {
        var pages = Controllers().Where(t => !t.IsDefined(typeof(ApiControllerAttribute), inherit: true)).ToList();

        Assert.NotEmpty(pages);
        Assert.All(pages, t => Assert.True(typeof(MvcControllerBase).IsAssignableFrom(t), $"{t.Name} debe heredar MvcControllerBase"));
        Assert.True(typeof(MvcControllerBase).IsDefined(typeof(AutoValidateAntiforgeryTokenAttribute), inherit: false));
        Assert.True(typeof(MvcControllerBase).IsDefined(typeof(ApiExplorerSettingsAttribute), inherit: false));
    }

    [Fact]
    public void EveryPostAction_OfThePages_IsCoveredByAntiforgery_AndNeverIgnoresIt()
    {
        var ignoring = Controllers()
            .Where(t => typeof(MvcControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.IsDefined(typeof(IgnoreAntiforgeryTokenAttribute), inherit: true))
            .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
            .ToList();

        Assert.Empty(ignoring);
    }
}
