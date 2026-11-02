using System.Linq.Expressions;
using System.Reflection;
using Availability.Application.Specification;
using Availability.Domain.Entities;

namespace Availability.Tests.Unit;

/// <summary>
/// Convención de la carpeta <c>Application/Specification</c>: una clase estática por entidad, con miembros que devuelven
/// <c>Expression&lt;Func&lt;Entidad,bool&gt;&gt;</c> (más, a lo sumo, datos de apoyo de solo lectura).
/// </summary>
public class SpecificationConventionTests
{
    private static readonly Type[] SpecificationClasses = typeof(SeatSpecification).Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(SeatSpecification).Namespace && t.IsClass && !t.IsNested)
        .ToArray();

    private static readonly Type[] Entities = typeof(Seat).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(EntityBase).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void TheFolderHasSpecifications_ForBothEntities()
    {
        Assert.Contains(typeof(SeatSpecification), SpecificationClasses);
        Assert.Contains(typeof(SeatRequestSpecification), SpecificationClasses);
    }

    [Fact]
    public void EverySpecificationClass_IsStatic()
    {
        Assert.All(SpecificationClasses, t => Assert.True(t.IsAbstract && t.IsSealed, $"{t.Name} debe ser static"));
    }

    [Fact]
    public void EverySpecificationClass_IsNamedAfterAnEntity()
    {
        Assert.All(SpecificationClasses, t =>
        {
            Assert.EndsWith("Specification", t.Name);
            var entityName = t.Name[..^"Specification".Length];
            Assert.Contains(Entities, e => e.Name == entityName);
        });
    }

    [Fact]
    public void EveryPublicMethod_ReturnsAnExpressionPredicate_OverItsOwnEntity()
    {
        foreach (var spec in SpecificationClasses)
        {
            var entity = Entities.Single(e => e.Name == spec.Name[..^"Specification".Length]);
            var expected = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(entity, typeof(bool)));

            var methods = spec.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.NotEmpty(methods);
            Assert.All(methods, m => Assert.True(m.ReturnType == expected,
                $"{spec.Name}.{m.Name} debe devolver {expected.Name}, devuelve {m.ReturnType.Name}"));
        }
    }

    [Fact]
    public void ApartFromMethods_OnlyReadOnlySupportDataIsExposed()
    {
        foreach (var spec in SpecificationClasses)
        {
            var fields = spec.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.All(fields, f => Assert.True(f.IsInitOnly || f.IsLiteral, $"{spec.Name}.{f.Name} debe ser de solo lectura"));
            Assert.Empty(spec.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(p => p.CanWrite));
        }
    }
}
