﻿// -----------------------------------------------------------------------
//  <copyright file="FunctionSyntaxNodeBase.cs">
//      Copyright (c) 2021 Dean Edis. All rights reserved.
//  </copyright>
//  <summary>
//  This example is provided on an "as is" basis and without warranty of any kind.
//  We do not warrant or make any representations regarding the use or
//  results of use of this example.
//  </summary>
// -----------------------------------------------------------------------

using System.Linq;
using Shrinker.Lexer;

namespace Shrinker.Parser.SyntaxNodes
{
    public abstract class FunctionSyntaxNodeBase : SyntaxNode
    {
        public string ReturnType { get; protected set; }
        public string Name => Children[0].Token.Content;
        public RoundBracketSyntaxNode Params => (RoundBracketSyntaxNode)Children[1];
        public bool HasOutParam => Params.TheTree.Select(o => o.Token as TypeToken).Any(o => o?.InOut == TypeToken.InOutType.InOut || o?.InOut == TypeToken.InOutType.Out);

        public bool IsVoidParam() => Params.Children.Count == 1 && Params.Children[0].HasNodeContent("void");

        public bool IsMain() => Name.StartsWith("main");
    }
}