using Cysharp.Threading.Tasks;
using RMC.Core.UI.DialogSystem;
using System;
using TezosAPI;
using TezosSDKExamples.Scenes;
using TezosSDKExamples.Shared.Tezos;
using TezosSDKExamples.View;
using UnityEngine;

#pragma warning disable CS4014, CS1998, CS0219
namespace TezosSDKExamples.Controllers
{
    /// <summary>
    /// Controller for <see cref="Example03_PlayerInventory"/>
    /// </summary>
    public class Example03_Parent : MonoBehaviour
    {
        //  Events ----------------------------------------


        //  Properties ------------------------------------
        protected Example03_View View { get { return _view; } }


        //  Fields ----------------------------------------
        [SerializeField]
        private Example03_View _view;


        //  Unity Methods  --------------------------------
        protected virtual async void Start()
        {
            // Header
            _view.HeaderTextFieldUI.IsVisible = true;
            _view.HeaderTextFieldUI.Text.text = "Player Inventory";

            // Body
            _view.MainTextPanelUI.IsVisible = true;
            _view.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "Click the button below to load your NFT inventory.";

            // Footer
            _view.RefreshInventoryButtonUI.Text.text = "Refresh Inventory";
            _view.RefreshInventoryButtonUI.Button.onClick.AddListener(() => OnRefreshInventoryButtonClicked());

            await RefreshUIAsync();
        }


        //  Methods ---------------------------------------
        protected virtual async UniTask RefreshUIAsync()
        {
            // Check whether the player has an active wallet session.
            // Inventory fetching requires a connected wallet address.
            ITezosAPI tezos = TezosSingleton.Instance;
            bool isAuthenticated = tezos.HasActiveWalletAddress();

            // Body
            if (isAuthenticated)
            {
                string address = tezos.GetActiveWalletAddress();
                _view.MainTextPanelUI.BodyTextAreaUI.Text.text =
                    $"The <b>Tezos SDK For Unity</b> can list all NFTs owned by wallet <b>{address}</b>.";
            }
            else
            {
                _view.MainTextPanelUI.BodyTextAreaUI.Text.text =
                    TezosSDKExamplesConstants.PleaseVisitAuthenticationSceneMessage;
            }

            // Footer — hide inventory UI until the player is authenticated
            _view.DetailsTextPanelUI.IsVisible = isAuthenticated;
            _view.DetailsTextPanelUI.BodyTextAreaUI.IsVisible = isAuthenticated;
            _view.RefreshInventoryButtonUI.IsVisible = isAuthenticated;
            _view.RefreshInventoryButtonUI.IsInteractable = isAuthenticated;
        }

        protected async UniTask ShowDialogAsync(string dialogTitle, Func<UniTask> transactionCall)
        {
            DialogData dialogData = TezosSDKExamplesConstants.CreateNewDialogData(dialogTitle);
            await _view.DialogSystem.ShowDialogAsync(
                dialogData,
                transactionCall,
                async () =>
                {
                    await RefreshUIAsync();
                });
        }


        //  Event Handlers --------------------------------
        protected virtual async UniTask OnRefreshInventoryButtonClicked()
        {
            // Required: Render UI
            TezosSDKExamplesHelper.PlayAudioClipClick01();
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "";
            await RefreshUIAsync();
        }
    }
}
