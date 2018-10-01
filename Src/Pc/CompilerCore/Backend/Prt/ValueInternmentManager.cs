﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Pc.TypeChecker.AST.Declarations;

namespace Microsoft.Pc.Backend.Prt
{
    internal class ValueInternmentManager<T>
    {
        private readonly PrtNameManager nameManager;
        private readonly string typeName = typeof(T).Name.ToUpper();
        private readonly IDictionary<Function, IDictionary<T, string>> valueInternmentTable;

        public ValueInternmentManager(PrtNameManager nameManager)
        {
            this.nameManager = nameManager;
            valueInternmentTable = new Dictionary<Function, IDictionary<T, string>>();
        }

        public string RegisterValue(Function function, T value)
        {
            if (!valueInternmentTable.TryGetValue(function, out var funcTable))
            {
                funcTable = new Dictionary<T, string>();
                valueInternmentTable.Add(function, funcTable);
            }

            if (!funcTable.TryGetValue(value, out string literalName))
            {
                literalName = nameManager.GetTemporaryName($"LIT_{typeName}");
                funcTable.Add(value, literalName);
            }

            return literalName;
        }

        public IEnumerable<KeyValuePair<T, string>> GetValues(Function function)
        {
            if (valueInternmentTable.TryGetValue(function, out var table))
            {
                return table.AsEnumerable();
            }

            return Enumerable.Empty<KeyValuePair<T, string>>();
        }
    }
}