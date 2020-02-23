using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class UdonSharpFieldVisitor : CSharpSyntaxWalker
{
    public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;

    public UdonSharpFieldVisitor(HashSet<FieldDeclarationSyntax> fieldsWithInitializers)
    {
        this.fieldsWithInitializers = fieldsWithInitializers;
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variables = node.Declaration.Variables;
        for (int i = 0; i < variables.Count; ++i)
        {
            VariableDeclaratorSyntax variable = variables[i];

            if (variable.Initializer != null)
            {
                fieldsWithInitializers.Add(node);
            }
        }
    }
}