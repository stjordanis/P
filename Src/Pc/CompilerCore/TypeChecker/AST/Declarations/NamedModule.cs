﻿using Antlr4.Runtime;
using Microsoft.Pc.TypeChecker.Types;

namespace Microsoft.Pc.TypeChecker.AST.Declarations
{
    public class NamedModule : IPDecl
    {
        public NamedModule(ParserRuleContext sourceNode, string moduleName)
        {
            Name = moduleName;
            SourceLocation = sourceNode;
        }

        public IPModuleExpr ModExpr { get; set; }
        public string Name { get; }
        public ParserRuleContext SourceLocation { get; }
    }
}