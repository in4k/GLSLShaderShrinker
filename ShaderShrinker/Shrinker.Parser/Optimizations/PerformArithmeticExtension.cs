﻿// -----------------------------------------------------------------------
//  <copyright file="PerformArithmeticExtension.cs">
//      Copyright (c) 2021 Dean Edis. All rights reserved.
//  </copyright>
//  <summary>
//  This example is provided on an "as is" basis and without warranty of any kind.
//  We do not warrant or make any representations regarding the use or
//  results of use of this example.
//  </summary>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Shrinker.Lexer;
using Shrinker.Parser.SyntaxNodes;

namespace Shrinker.Parser.Optimizations
{
    public static class PerformArithmeticExtension
    {
        private const int MaxDp = 5;

        /// <summary>
        /// Returns true if all optimizations should be re-run.
        /// </summary>
        public static bool PerformArithmetic(this SyntaxNode rootNode)
        {
            var repeatSimplifications = false;

            while (true)
            {
                var didChange = false;

                // 'a = b + -c' => 'a = b - c'
                foreach (var symbolNode in rootNode.TheTree
                    .OfType<GenericSyntaxNode>()
                    .Where(
                           o => (o.Token as SymbolOperatorToken)?.Content == "+" &&
                                (o.Next?.Token as SymbolOperatorToken)?.Content == "-")
                    .ToList())
                {
                    symbolNode.Remove();
                    didChange = true;
                }

                // 'f + -2.3' => 'f - 2.3'
                foreach (var numNode in rootNode.TheTree
                    .OfType<GenericSyntaxNode>()
                    .Where(
                           o => o.Token is INumberToken &&
                                o.Token.Content.StartsWith("-") &&
                                o.Previous?.Token.GetMathSymbolType() == TokenExtensions.MathSymbolType.AddSubtract)
                    .ToList())
                {
                    var symbol = numNode.Previous.Token.Content[0] == '-' ? "+" : "-";
                    numNode.Previous.ReplaceWith(new GenericSyntaxNode(new SymbolOperatorToken(symbol)));
                    ((INumberToken)numNode.Token).MakePositive();
                    didChange = true;
                }

                // 'f * 1.0' or 'f / 1.0' => 'f'
                foreach (var oneNode in rootNode.TheTree
                    .OfType<GenericSyntaxNode>()
                    .Where(
                           o => (o.Token as INumberToken)?.IsOne() == true &&
                                o.Previous?.Token.GetMathSymbolType() == TokenExtensions.MathSymbolType.MultiplyDivide)
                    .ToList())
                {
                    oneNode.Previous.Remove();
                    oneNode.Remove();
                    didChange = true;
                }
                
                // 'f / 0.0' => 'f * 0.0'
                foreach (var zeroNode in rootNode.TheTree
                    .OfType<GenericSyntaxNode>()
                    .Where(
                           o => (o.Token as INumberToken)?.IsZero() == true &&
                                o.Previous?.Token.Content == "/")
                    .ToList())
                {
                    zeroNode.Previous.ReplaceWith(new GenericSyntaxNode(new SymbolOperatorToken("*")));
                    didChange = true;
                }
                
                // 'f * 0.0' => <Nothing>
                if (!didChange)
                {
                    foreach (var zeroNode in rootNode.TheTree
                        .OfType<GenericSyntaxNode>()
                        .Where(
                               o => (o.Token as INumberToken)?.IsZero() == true &&
                                    o.Previous?.Token.Content == "*" &&
                                    (o.Next == null || o.Next?.Token is CommaToken))
                        .ToList())
                    {
                        var f = zeroNode.Previous.Previous;
                        if ((f as GenericSyntaxNode)?.Token is AlphaNumToken ||
                            f is FunctionCallSyntaxNode call && !call.HasOutParam)
                        {
                            f.Remove();
                            zeroNode.Previous.Remove();
                            didChange = true;
                        }
                    }
                }
                
                // 'f + 0.0' => f
                if (!didChange)
                {
                    foreach (var zeroNode in rootNode.TheTree
                        .OfType<GenericSyntaxNode>()
                        .Where(
                               o => (o.Token as INumberToken)?.IsZero() == true &&
                                    o.Previous?.Token.GetMathSymbolType() == TokenExtensions.MathSymbolType.AddSubtract &&
                                    (o.Next == null || o.Next?.Token is CommaToken))
                        .ToList())
                    {
                        zeroNode.Previous.Remove();
                        zeroNode.Remove();
                        didChange = true;
                    }
                }

                // pow(1.1, 2.2) => <the result>
                foreach (var powNode in rootNode.TheTree
                    .OfType<GlslFunctionCallSyntaxNode>()
                    .Where(o => o.Name == "pow" && o.Params.IsSimpleCsv())
                    .ToList())
                {
                    var xy = powNode.Params.Children.Where(o => o.Token is FloatToken).Select(o => (FloatToken)o.Token).ToList();
                    if (xy.Count == 2 && xy.All(o => o.Number > 0.0))
                    {
                        powNode.Params.Remove();
                        powNode.ReplaceWith(new GenericSyntaxNode(new FloatToken($"{Math.Pow(xy[0].Number, xy[1].Number):F}").Simplify()));
                        didChange = true;
                    }
                }

                // Perform simple arithmetic calculations.
                foreach (var numNodeA in rootNode.TheTree
                    .OfType<GenericSyntaxNode>()
                    .Where(
                           o => o.Token is INumberToken &&
                                o.Next?.Token?.GetMathSymbolType() != TokenExtensions.MathSymbolType.Unknown &&
                                o.Next?.Next?.Token is INumberToken)
                    .Reverse()
                    .ToList())
                {
                    var symbolNode = numNodeA.Next;
                    var symbolType = symbolNode.Token.GetMathSymbolType();

                    if (symbolType != TokenExtensions.MathSymbolType.MultiplyDivide &&
                        numNodeA.Previous != null &&
                        numNodeA.Previous.Token?.GetMathSymbolType() != symbolType)
                    {
                        continue;
                    }

                    var numNodeB = symbolNode.Next;
                    if (numNodeB?.Next?.Token is SymbolOperatorToken &&
                        numNodeB.Next.Token.GetMathSymbolType() != TokenExtensions.MathSymbolType.AddSubtract)
                    {
                        continue;
                    }

                    var a = double.Parse(numNodeA.Token.Content);
                    var b = double.Parse(numNodeB.Token.Content);

                    var symbol = symbolNode.Token.Content[0];

                    // Invert * or / if preceded by a /.
                    // E.g. 1.2 / 2.3 * 4.5 = 1.2 / (2.3 / 4.5)
                    //                      = 1.2 / 0.51111
                    //                      = 2.3478
                    if (numNodeA.Previous?.Token?.GetMathSymbolType() == TokenExtensions.MathSymbolType.MultiplyDivide &&
                        numNodeA.Previous.HasNodeContent("/"))
                    {
                        symbol = symbol == '*' ? '/' : '*';
                    }
                    
                    // Invert + or - if preceded by a -.
                    // E.g. -3.0 + 0.1 = - (3.0 - 0.1)
                    //                 = - (2.9)
                    //                 = -2.9
                    if (numNodeA.Previous.HasNodeContent("-") &&
                        symbolNode.Token.GetMathSymbolType() == TokenExtensions.MathSymbolType.AddSubtract)
                    {
                        symbol = symbol == '+' ? '-' : '+';
                    }

                    double c;
                    switch (symbol)
                    {
                        case '+':
                            c = a + b;
                            break;
                        case '-':
                            c = a - b;
                            break;
                        case '*':
                            c = a * b;
                            break;
                        case '/':
                            c = a / b;
                            if (double.IsInfinity(c))
                                c = 0.0;
                            break;
                        default:
                            throw new InvalidOperationException($"Unrecognized math operation '{symbol}'.");
                    }

                    var isFloatResult = numNodeA.Token is FloatToken || numNodeB.Token is FloatToken;
                    numNodeB.Remove();
                    symbolNode.Remove();

                    SyntaxNode newNode;
                    if (isFloatResult)
                    {
                        var numberToken = Math.Abs(c) < 0.0001 && Math.Abs(c) > 0.0 ? new FloatToken(c.ToString($".#{new string('#', MaxDp - 1)}e0")) : new FloatToken(c.ToString($"F{MaxDp}"));
                        newNode = numNodeA.ReplaceWith(new GenericSyntaxNode(numberToken.Simplify()));
                    }
                    else
                    {
                        newNode = numNodeA.ReplaceWith(new GenericSyntaxNode(new IntToken((int)c)));
                    }

                    // If new node is the sole child of a group, promote it.
                    if (newNode.IsOnlyChild() && newNode.Parent.GetType() == typeof(GroupSyntaxNode))
                        newNode.Parent.ReplaceWith(newNode);

                    didChange = true;
                }

                if (!didChange)
                    break;

                repeatSimplifications = true;
            }

            return repeatSimplifications;
        }
    }
}