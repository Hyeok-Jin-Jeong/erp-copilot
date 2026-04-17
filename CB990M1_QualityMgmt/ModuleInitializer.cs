using Bizentro.AppFramework.UI.Module;
using Microsoft.Practices.CompositeUI;
using Microsoft.Practices.ObjectBuilder;

namespace Bizentro.App.UI.PP.CB990M1_CKO087
{
    /// <summary>
    /// CB990M1_CKO087 모듈 초기화
    /// UNIERP Shell 로드 시 자동 호출 → ModuleViewer를 WorkItem에 등록
    /// </summary>
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
