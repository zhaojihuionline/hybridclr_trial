﻿using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HybridCLR.Editor.Meta
{
    public abstract class AssemblyCacheBase : IDisposable
    {
        private readonly IAssemblyResolver _assemblyPathResolver;
        private readonly ModuleContext _modCtx;
        private readonly AssemblyResolver _asmResolver;
        private bool disposedValue;
        private bool _loadedNetstandard;

        public Dictionary<string, ModuleDefMD> LoadedModules { get; } = new Dictionary<string, ModuleDefMD>();

        private readonly List<ModuleDefMD> _loadedModulesIncludeNetstandard = new List<ModuleDefMD>();

        protected AssemblyCacheBase(IAssemblyResolver assemblyResolver)
        {
            _assemblyPathResolver = assemblyResolver;
            _modCtx = ModuleDef.CreateModuleContext();
            _asmResolver = (AssemblyResolver)_modCtx.AssemblyResolver;
            _asmResolver.EnableTypeDefCache = true;
            _asmResolver.UseGAC = false;
        }

        public ModuleDefMD LoadModule(string moduleName, bool loadReferenceAssemblies = true, bool notThrowErrorWhenNotFound = false)
        {
            // Debug.Log($"load module:{moduleName}");
            if (LoadedModules.TryGetValue(moduleName, out var mod))
            {
                return mod;
            }
            if (moduleName == "netstandard")
            {
                if (!_loadedNetstandard)
                {
                    LoadNetStandard();
                }
                return null;
            }

            string assPath = _assemblyPathResolver.ResolveAssembly(moduleName, !notThrowErrorWhenNotFound);
            if (string.IsNullOrEmpty(assPath))
            {
                if (notThrowErrorWhenNotFound)
                {
                    return null;
                }
                throw new Exception($"can not find assembly:{moduleName}");
            }
            mod = DoLoadModule(assPath);
            LoadedModules.Add(moduleName, mod);

            if (loadReferenceAssemblies)
            {
                foreach (var refAsm in mod.GetAssemblyRefs())
                {
                    LoadModule(refAsm.Name);
                }
            }

            return mod;
        }

        private void LoadNetStandard()
        {
            string netstandardDllPath = _assemblyPathResolver.ResolveAssembly("netstandard", false);
            if (!string.IsNullOrEmpty(netstandardDllPath))
            {
                DoLoadModule(netstandardDllPath);
            }
            else
            {
                DoLoadModule(MetaUtil.ResolveNetStandardAssemblyPath("netstandard2.0"));
                DoLoadModule(MetaUtil.ResolveNetStandardAssemblyPath("netstandard2.1"));
            }
            _loadedNetstandard = true;
        }

        private ModuleDefMD DoLoadModule(string dllPath)
        {
            //Debug.Log($"do load module:{dllPath}");
            ModuleDefMD mod = ModuleDefMD.Load(dllPath, _modCtx);
            _asmResolver.AddToCache(mod);
            _loadedModulesIncludeNetstandard.Add(mod);
            return mod;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var mod in _loadedModulesIncludeNetstandard)
                    {
                        mod.Dispose();
                    }
                    _loadedModulesIncludeNetstandard.Clear();
                    LoadedModules.Clear();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
