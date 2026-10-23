using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace DevExtreme.AspNet.Data.Helpers {

    public static class CustomAccessorCompilers {
        public delegate Expression CompilerFunc(Expression expr, string accessorText);
        public delegate Expression CompilerFuncWithContext(Expression expr, string accessorText, object runtimeContext);

        static readonly ICollection<CompilerFuncWithContext> _compilersWithContext = new List<CompilerFuncWithContext>();

        public static void Register(CompilerFunc compilerFunc) {
            _compilersWithContext.Add((target, accessorText, runtimeContext) => compilerFunc(target, accessorText));
        }

        public static void RegisterWithContext(CompilerFuncWithContext compilerFunc) {
            _compilersWithContext.Add(compilerFunc);
        }

        internal static Expression TryCompile(Expression expr, string accessorText, object runtimeContext) {
            if(_compilersWithContext.Count >= 1) {
                foreach(var compiler in _compilersWithContext) {
                    var result = compiler(expr, accessorText, runtimeContext);
                    if(result != null)
                        return result;
                }
            }

            return null;
        }

#if DEBUG
        internal static void Clear() {
            _compilersWithContext.Clear();
        }
#endif

    }

}
