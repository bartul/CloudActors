﻿using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static Devlooped.CloudActors.Diagnostics;

namespace Devlooped.CloudActors;

[Generator(LanguageNames.CSharp)]
public class ActorCommandGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var source = context.CompilationProvider.SelectMany((x, _) => x.Assembly.GetAllTypes())
            .Where(x => x.GetAttributes().Any(IsActorOperation))
            .Combine(context.CompilationProvider)
            .Select((x, _) =>
            {
                if (x.Left is not INamedTypeSymbol command ||
                    !IsPartial(command) ||
                    command.GetAttributes().FirstOrDefault(IsActorOperation) is not AttributeData attr ||
                    attr.ApplicationSyntaxReference?.GetSyntax() is not SyntaxNode syntax ||
                    x.Right.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not IAttributeOperation operation ||
                    operation.Operation.Type is not INamedTypeSymbol type)
                    return default;

                return new { Operation = command, Attribute = type };
            });

        context.RegisterSourceOutput(source, (ctx, item) =>
        {
            if (item is null)
                return;

            if (item.Attribute.IsGenericType)
            {
                var result = item.Attribute.TypeArguments[0];
                var flavor = item.Attribute.Name == "ActorQueryAttribute" ? "Query" : "Command";

                ctx.AddSource($"{item.Operation.ToFileName()}.g.cs",
                    $$"""
                    using Devlooped.CloudActors;

                    namespace {{item.Operation.ContainingNamespace.ToDisplayString(FullName)}}
                    {
                        partial record {{item.Operation.Name}} : IActor{{flavor}}<{{result.ToDisplayString(FullName)}}>;
                    }
                    """);
            }
            else
            {
                ctx.AddSource($"{item.Operation.ToFileName()}.g.cs",
                    $$"""
                    using Devlooped.CloudActors;

                    namespace {{item.Operation.ContainingNamespace.ToDisplayString(FullName)}}
                    {
                        partial record {{item.Operation.Name}} : IActorCommand;
                    }
                    """);
            }
        });
    }
}
