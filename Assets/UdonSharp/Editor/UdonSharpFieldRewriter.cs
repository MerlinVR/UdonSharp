using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        for (int i = 0; i < variables.Count; ++i)
        {
            VariableDeclaratorSyntax variable = variables[i];

            if (variable.Initializer != null)
            {
                variables = variables.Replace(variable, variable.WithInitializer(null));
                fieldsWithInitializers.Add(node);
            }
        }

        return node.WithDeclaration(node.Declaration.WithVariables(variables));
    }
}