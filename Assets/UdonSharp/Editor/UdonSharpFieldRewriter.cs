using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

public class UdonSharpFieldRewriter : CSharpSyntaxRewriter
{
    public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;

    public UdonSharpFieldRewriter(HashSet<FieldDeclarationSyntax> fieldsWithInitializers)
    {
        this.fieldsWithInitializers = fieldsWithInitializers;
    }

    public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variables = node.Declaration.Variables;
        foreach (var variable in node.Declaration.Variables)
        {
            if (variable.Initializer != null)
            {
                variables = variables.Replace(variable, variable.WithInitializer(null));
                fieldsWithInitializers.Add(node);
            }
        }

        return node.WithDeclaration(node.Declaration.WithVariables(variables));
    }
}