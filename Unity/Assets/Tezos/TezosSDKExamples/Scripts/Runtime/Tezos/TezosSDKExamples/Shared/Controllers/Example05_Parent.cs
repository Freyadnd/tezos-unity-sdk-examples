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
    /// Controller for <see cref="Example05_Leaderboard"/>
    /// </summary>
    public class Example05_Parent : MonoBehaviour
    {
        //  Events ----------------------------------------


        //  Properties ------------------------------------
        protected Example05_View View { get { return _view; } }


        //  Fields ----------------------------------------
        [SerializeField]
        private Example05_View _view;


        //  Unity Methods  --------------------------------
        protected virtual async void Start()
        {
            // Header
            _view.HeaderTextFieldUI.IsVisible = true;
            _view.HeaderTextFieldUI.Text.text = "Blockchain Leaderboard";

            // Body
            _view.MainTextPanelUI.IsVisible = true;
            _view.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "Connect a wallet to submit your score or view the leaderboard.";

            // Footer
            _view.SubmitScoreButtonUI.Text.text     = "Submit Score";
            _view.ViewLeaderboardButtonUI.Text.text = "View Leaderboard";
            _view.SubmitScoreButtonUI.Button.onClick.AddListener(()     => OnSubmitScoreButtonClicked());
            _view.ViewLeaderboardButtonUI.Button.onClick.AddListener(() => OnViewLeaderboardButtonClicked());

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
                    $"The <b>Tezos SDK For Unity</b> can submit scores on-chain from wallet <b>{address}</b>.";
            }
            else
            {
                _view.MainTextPanelUI.BodyTextAreaUI.Text.text =
                    TezosSDKExamplesConstants.PleaseVisitAuthenticationSceneMessage;
            }

            // Footer — score submission requires authentication; leaderboard is public
            _view.DetailsTextPanelUI.IsVisible              = true;
            _view.DetailsTextPanelUI.BodyTextAreaUI.IsVisible = true;
            _view.SubmitScoreButtonUI.IsVisible             = isAuthenticated;
            _view.SubmitScoreButtonUI.IsInteractable        = isAuthenticated;
            _view.ViewLeaderboardButtonUI.IsVisible         = true;
            _view.ViewLeaderboardButtonUI.IsInteractable    = true;
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
        protected virtual async UniTask OnSubmitScoreButtonClicked()
        {
            TezosSDKExamplesHelper.PlayAudioClipClick01();
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "";
            await RefreshUIAsync();
        }

        protected virtual async UniTask OnViewLeaderboardButtonClicked()
        {
            TezosSDKExamplesHelper.PlayAudioClipClick01();
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = "";
            await RefreshUIAsync();
        }
    }
}
