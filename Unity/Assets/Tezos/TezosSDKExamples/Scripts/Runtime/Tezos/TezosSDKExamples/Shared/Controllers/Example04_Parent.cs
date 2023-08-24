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
    /// Controller for <see cref="Example04_FA2Transfer"/>
    /// </summary>
    public class Example04_Parent : MonoBehaviour
    {
        //  Events ----------------------------------------


        //  Properties ------------------------------------
        protected Example04_View View { get { return _view; } }


        //  Fields ----------------------------------------
        [SerializeField]
        private Example04_View _view;


        //  Unity Methods  --------------------------------
        protected virtual async void Start()
        {
            // Header
            _view.HeaderTextFieldUI.IsVisible = true;
            _view.HeaderTextFieldUI.Text.text = "FA2 Token Transfer";

            // Body
            _view.MainTextPanelUI.IsVisible = true;
            _view.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "Click the button below to send an FA2 token transfer.";

            // Footer
            _view.TransferButtonUI.Text.text = "Transfer Token";
            _view.TransferButtonUI.Button.onClick.AddListener(() => OnTransferButtonClicked());

            await RefreshUIAsync();
        }


        //  Methods ---------------------------------------
        protected virtual async UniTask RefreshUIAsync()
        {
            ITezosAPI tezos = TezosSingleton.Instance;
            bool isAuthenticated = tezos.HasActiveWalletAddress();

            // Body
            if (isAuthenticated)
            {
                string address = tezos.GetActiveWalletAddress();
                _view.MainTextPanelUI.BodyTextAreaUI.Text.text =
                    $"The <b>Tezos SDK For Unity</b> can transfer FA2 tokens from wallet <b>{address}</b>.";
            }
            else
            {
                _view.MainTextPanelUI.BodyTextAreaUI.Text.text =
                    TezosSDKExamplesConstants.PleaseVisitAuthenticationSceneMessage;
            }

            // Footer — hide transfer UI until the player is authenticated
            _view.DetailsTextPanelUI.IsVisible = isAuthenticated;
            _view.DetailsTextPanelUI.BodyTextAreaUI.IsVisible = isAuthenticated;
            _view.TransferButtonUI.IsVisible = isAuthenticated;
            _view.TransferButtonUI.IsInteractable = isAuthenticated;
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
        protected virtual async UniTask OnTransferButtonClicked()
        {
            // Required: Render UI
            TezosSDKExamplesHelper.PlayAudioClipClick01();
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "";
            await RefreshUIAsync();
        }
    }
}
