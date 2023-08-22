using RMC.Core.UI;
using TezosSDKExamples.Scenes;
using UnityEngine;

#pragma warning disable CS1998
namespace TezosSDKExamples.View
{
    /// <summary>
    /// UI for <see cref="Example03_PlayerInventory"/>
    /// </summary>
    public class Example03_View : Scene_BaseView
    {
        //  Events ----------------------------------------

        //  Properties ------------------------------------
        public ButtonUI RefreshInventoryButtonUI { get { return _refreshInventoryButtonUI; } }

        //  Fields ----------------------------------------
        [Header("Child")]

        [SerializeField]
        private ButtonUI _refreshInventoryButtonUI;

        //  Unity Methods  --------------------------------
        protected override async void Awake()
        {
            base.Awake();
        }

        protected override async void Start()
        {
            base.Start();
        }

        //  Methods ---------------------------------------

        //  Event Handlers --------------------------------
    }
}
