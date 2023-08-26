using RMC.Core.UI;
using TezosSDKExamples.Scenes;
using UnityEngine;

#pragma warning disable CS1998
namespace TezosSDKExamples.View
{
    /// <summary>
    /// UI for <see cref="Example05_Leaderboard"/>
    /// </summary>
    public class Example05_View : Scene_BaseView
    {
        //  Events ----------------------------------------

        //  Properties ------------------------------------
        public ButtonUI SubmitScoreButtonUI     { get { return _submitScoreButtonUI; } }
        public ButtonUI ViewLeaderboardButtonUI { get { return _viewLeaderboardButtonUI; } }

        //  Fields ----------------------------------------
        [Header("Child")]

        [SerializeField]
        private ButtonUI _submitScoreButtonUI;

        [SerializeField]
        private ButtonUI _viewLeaderboardButtonUI;

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
