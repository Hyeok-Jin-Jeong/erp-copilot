using Bizentro.AppFramework.UI.Module;
using Microsoft.Practices.CompositeUI;
using Microsoft.Practices.ObjectBuilder;

namespace Bizentro.App.UI.PP.PA999M1_CKO087
{
    public class ModuleInitializer : Bizentro.AppFramework.UI.Module.Module
    {
        [InjectionConstructor]
        public ModuleInitializer([ServiceDependency] WorkItem rootWorkItem)
            : base(rootWorkItem) { }

        protected override void RegisterModureViewer()
        {
            base.AddModule<ModuleViewer>();
        }
    }
}
