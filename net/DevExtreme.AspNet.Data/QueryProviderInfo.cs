﻿using System;
using System.Linq;
using System.Reflection;

namespace DevExtreme.AspNet.Data {

    class QueryProviderInfo {
        public readonly bool IsLinqToObjects;
        public readonly bool IsEFClassic;
        public readonly bool IsEFCore;
        public readonly bool IsXPO;
        public readonly bool IsNH;
        public readonly bool IsL2S;
        public readonly bool IsMongoDB;
        public readonly Version Version;

        public QueryProviderInfo(IQueryProvider provider) {
            if(provider is EnumerableQuery) {
                IsLinqToObjects = true;
            } else {
                var type = provider.GetType();
                var typeName = type.FullName;
                var providerAssembly = type.Assembly;

                if(typeName == "Microsoft.Data.Entity.Query.Internal.EntityQueryProvider" || typeName == "System.Data.Entity.Internal.Linq.DbQueryProvider")
                    IsEFClassic = true;
                else if(typeName == "Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryProvider")
                    IsEFCore = true;
                else if(typeName.StartsWith("DevExpress.Xpo.XPQuery"))
                    IsXPO = true;
                else if(typeName.StartsWith("NHibernate.Linq."))
                    IsNH = true;
                else if(typeName.StartsWith("System.Data.Linq."))
                    IsL2S = true;
                else if(typeName.StartsWith("MongoDB.Driver.Linq."))
                    IsMongoDB = true;
                else if(typeName.StartsWith("LinqKit.ExpandableQueryProvider`1")) {
                    if(TryAssembly("Microsoft.EntityFrameworkCore", ref providerAssembly)) {
                        IsEFCore = true;
                    } else if(TryAssembly("EntityFramework", ref providerAssembly)) {
                        IsEFClassic = true;
                    }
                }

                Version = providerAssembly.GetName().Version;
            }
        }

        bool TryAssembly(string name, ref Assembly result) {
            try {
                result = AppDomain.CurrentDomain.GetAssemblies().First(i => i.GetName().Name == name);
                return true;
            } catch {
                return false;
            }
        }
    }

}
