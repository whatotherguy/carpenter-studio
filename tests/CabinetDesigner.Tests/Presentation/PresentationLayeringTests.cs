using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class PresentationLayeringTests
{
    [Fact]
    public void EditorCanvasSessionAdapter_DoesNotImportDomainNamespaces()
    {
        var adapterType = typeof(EditorCanvasSessionAdapter);

        foreach (var memberType in EnumerateMemberTypes(adapterType))
        {
            Assert.False(
                IsDomainType(memberType),
                $"Unexpected domain type on adapter: {memberType.FullName}");
        }
    }

    [Fact]
    public void Presentation_Csproj_DoesNotReferenceDomainProject()
    {
        var csprojPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "CabinetDesigner.Presentation",
            "CabinetDesigner.Presentation.csproj"));

        var document = XDocument.Load(csprojPath);
        var projectReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.DoesNotContain(projectReferences, reference =>
            reference!.Contains("CabinetDesigner.Domain.csproj", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Type> EnumerateMemberTypes(Type type)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(flags))
        {
            yield return property.PropertyType;
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            if (method.IsSpecialName)
            {
                continue;
            }

            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static bool IsDomainType(Type type)
    {
        if (type.Namespace?.StartsWith("CabinetDesigner.Domain", StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (type.IsArray)
        {
            return IsDomainType(type.GetElementType()!);
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            if (IsDomainType(genericArgument))
            {
                return true;
            }
        }

        return false;
    }
}
