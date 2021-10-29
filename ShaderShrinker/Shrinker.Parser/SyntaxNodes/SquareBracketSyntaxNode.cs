﻿// -----------------------------------------------------------------------
//  <copyright file="SquareBracketSyntaxNode.cs">
//      Copyright (c) 2021 Dean Edis. All rights reserved.
//  </copyright>
//  <summary>
//  This code is provided on an "as is" basis and without warranty of any kind.
//  We do not warrant or make any representations regarding the use or
//  results of use of this code.
//  </summary>
// -----------------------------------------------------------------------

namespace Shrinker.Parser.SyntaxNodes
{
    public class SquareBracketSyntaxNode : GroupSyntaxNode
    {
        public override string UiName => "[...]";

        protected override SyntaxNode CreateSelf() => new SquareBracketSyntaxNode();
    }
}