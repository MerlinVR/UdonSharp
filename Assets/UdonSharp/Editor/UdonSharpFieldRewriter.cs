using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class UdonSharpFieldRewriter : CSharpSyntaxRewriter
{
    public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;
    public Dictionary<SyntaxNode, SyntaxNode> remappedSyntaxNodes = new Dictionary<SyntaxNode, SyntaxNode>();

    public UdonSharpFieldRewriter(HashSet<FieldDeclarationSyntax> fieldsWithInitializers)
    {
        this.fieldsWithInitializers = fieldsWithInitializers;
    }

    public override SyntaxNode DefaultVisit(SyntaxNode node)
    {
        SyntaxNode newNode = base.DefaultVisit(node);

        remappedSyntaxNodes.Add(newNode, node);

        return newNode;
    }

    public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        StringBuilder s = new StringBuilder();
        var variables = node.Declaration.Variables;
        for (int i = 0; i < variables.Count; ++i)
        {
            VariableDeclaratorSyntax variable = variables[i];

            if (variable.Initializer != null)
            {
                variables = variables.Replace(variable, variable.WithInitializer(null));
                fieldsWithInitializers.Add(node);
                s.Append(variable.Initializer.ToFullString());
            }
        }

        var newNode = node.WithDeclaration(node.Declaration.WithVariables(variables));
        if (s.Length > 0)
            newNode = newNode.WithTrailingTrivia(SyntaxFactory.Comment($"/*{s}*/"));

        remappedSyntaxNodes.Add(newNode, node);

        return newNode;
    }
}