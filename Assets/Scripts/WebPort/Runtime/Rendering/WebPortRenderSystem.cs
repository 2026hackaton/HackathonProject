using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortRenderSystem
    {
        private readonly Transform _root;
        private readonly Transform _playersRoot;
        private readonly Transform _packagesRoot;
        private readonly WebPortWorldView _worldView;
        private readonly WebPortEffectRenderer _effectRenderer;
        private readonly WebPortVehicleView _vehicleView;
        private readonly WebPortAimingView _aimingView;
        private readonly Dictionary<int, WebPortPlayerView> _playerViews = new();
        private readonly Dictionary<int, WebPortPackageView> _packageViews = new();

        public WebPortRenderSystem(Transform root)
        {
            _root = new GameObject("WebPort World").transform;
            _root.SetParent(root, false);
            _playersRoot = new GameObject("Players").transform;
            _playersRoot.SetParent(_root, false);
            _packagesRoot = new GameObject("Packages").transform;
            _packagesRoot.SetParent(_root, false);
            _worldView = new WebPortWorldView(_root);
            _effectRenderer = new WebPortEffectRenderer(_root);
            _vehicleView = new WebPortVehicleView(_root);
            _aimingView = new WebPortAimingView(_root);
        }

        public WebPortRenderSystem(WebPortSceneLayout layout)
        {
            _root = layout.WorldRoot;
            _playersRoot = layout.PlayersRoot;
            _packagesRoot = layout.PackagesRoot;
            _worldView = new WebPortWorldView(layout.StaticWorldRoot, layout.GoalMarker);
            _effectRenderer = new WebPortEffectRenderer(layout.EffectsRoot, true);
            _vehicleView = new WebPortVehicleView(layout.VehiclesRoot, layout.Truck, layout.Bus);
            _aimingView = new WebPortAimingView(layout.AimingRoot, true);
        }

        public Transform GetPlayerTransform(int playerId)
        {
            return _playerViews.TryGetValue(playerId, out WebPortPlayerView view) ? view.Transform : null;
        }

        public void Update(
            IReadOnlyDictionary<int, PlayerState> players,
            IReadOnlyDictionary<int, PackageState> packages,
            IReadOnlyList<EffectEvent> effects,
            Vector3 start,
            Vector3 goal,
            Vector3 mouseWorld,
            int selfId,
            UnityEngine.Camera camera,
            float now)
        {
            _worldView.SetGoal(goal);
            SyncPlayers(players, selfId);
            SyncPackages(packages);

            foreach (KeyValuePair<int, WebPortPlayerView> pair in _playerViews)
            {
                if (players.TryGetValue(pair.Key, out PlayerState player))
                    pair.Value.Update(player, packages.Values, camera, now);
            }

            foreach (KeyValuePair<int, WebPortPackageView> pair in _packageViews)
            {
                if (!packages.TryGetValue(pair.Key, out PackageState package))
                    continue;

                PlayerState holder = null;
                if (package.HeldBy.HasValue)
                    players.TryGetValue(package.HeldBy.Value, out holder);
                pair.Value.Update(package, holder);
            }

            _effectRenderer.Update(effects, now);
            _vehicleView.Update(start, goal);

            if (players.TryGetValue(selfId, out PlayerState self))
                _aimingView.Update(self, players, packages, mouseWorld);
        }

        public void PlayTruck()
        {
            _vehicleView.PlayTruck();
        }

        public void PlayBus()
        {
            _vehicleView.PlayBus();
        }

        private void SyncPlayers(IReadOnlyDictionary<int, PlayerState> players, int selfId)
        {
            List<int> remove = null;
            foreach (int id in _playerViews.Keys)
            {
                if (!players.ContainsKey(id))
                {
                    remove ??= new List<int>();
                    remove.Add(id);
                }
            }

            if (remove != null)
            {
                foreach (int id in remove)
                {
                    _playerViews[id].Destroy();
                    _playerViews.Remove(id);
                }
            }

            foreach (int id in players.Keys)
            {
                if (!_playerViews.ContainsKey(id))
                    _playerViews[id] = new WebPortPlayerView(_playersRoot, id == selfId, id == selfId ? "Player Self" : $"Player {id}");
            }
        }

        private void SyncPackages(IReadOnlyDictionary<int, PackageState> packages)
        {
            List<int> remove = null;
            foreach (int id in _packageViews.Keys)
            {
                if (!packages.ContainsKey(id))
                {
                    remove ??= new List<int>();
                    remove.Add(id);
                }
            }

            if (remove != null)
            {
                foreach (int id in remove)
                {
                    _packageViews[id].Destroy();
                    _packageViews.Remove(id);
                }
            }

            foreach (int id in packages.Keys)
            {
                if (!_packageViews.ContainsKey(id))
                    _packageViews[id] = new WebPortPackageView(_packagesRoot, id);
            }
        }
    }
}
