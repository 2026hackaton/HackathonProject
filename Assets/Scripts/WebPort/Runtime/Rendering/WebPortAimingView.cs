using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortAimingView
    {
        private const int DotCount = 14;
        private readonly Transform[] _dots = new Transform[DotCount];
        private readonly LineRenderer _grabbedLine;
        private readonly LineRenderer _draggingLine;

        public WebPortAimingView(Transform parent)
        {
            Transform root = new GameObject("Aiming").transform;
            root.SetParent(parent, false);

            Material dotMaterial = WebPortVisuals.CreateUnlit(WebPortVisuals.Html("#ff7043"));
            for (int i = 0; i < DotCount; i++)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dot.name = $"Throw Dot {i}";
                dot.transform.SetParent(root, false);
                dot.transform.localScale = Vector3.one * 7f;
                dot.GetComponent<MeshRenderer>().sharedMaterial = dotMaterial;
                Object.Destroy(dot.GetComponent<Collider>());
                dot.SetActive(false);
                _dots[i] = dot.transform;
            }

            _grabbedLine = CreateLine(root, "Grabbed Line");
            _draggingLine = CreateLine(root, "Dragging Line");
        }

        public void Update(PlayerState self, IReadOnlyDictionary<int, PlayerState> players, IReadOnlyDictionary<int, PackageState> packages, Vector3 mouseWorld)
        {
            UpdateThrowArc(self, packages.Values, mouseWorld);
            UpdateLine(_grabbedLine, self.GrabbedBy, self, players);
            UpdateLine(_draggingLine, self.DraggingId, self, players);
        }

        private void UpdateThrowArc(PlayerState self, IEnumerable<PackageState> packages, Vector3 mouseWorld)
        {
            PackageState heldPackage = packages.Where(p => p.HeldBy == self.Id).OrderBy(p => p.Id).FirstOrDefault();
            bool active = self.ChargingThrow && heldPackage != null;
            if (!active)
            {
                for (int i = 0; i < DotCount; i++)
                    _dots[i].gameObject.SetActive(false);
                return;
            }

            float held = Mathf.Min(Time.time - self.ThrowChargeStartedAt, WebPortConstants.MaxChargeSeconds);
            float power = (held / WebPortConstants.MaxChargeSeconds) * WebPortConstants.MaxPower;
            float angle = Mathf.Atan2(mouseWorld.z - self.Position.z, mouseWorld.x - self.Position.x);
            float vx = Mathf.Cos(angle) * power;
            float vz = Mathf.Sin(angle) * power;
            float vy = power * WebPortConstants.ThrowVyFactor;
            Vector3 origin = heldPackage.RenderPosition;
            float y0 = Mathf.Max(origin.y, 0f);
            float g = WebPortConstants.ThrowGravity;
            float disc = Mathf.Max(vy * vy + 2f * g * y0, 0f);
            float totalT = (vy + Mathf.Sqrt(disc)) / g;
            if (totalT <= 0.001f)
                totalT = 0.6f;

            for (int i = 0; i < DotCount; i++)
            {
                float t = (totalT * (i + 1)) / (DotCount + 1);
                Transform dot = _dots[i];
                dot.gameObject.SetActive(true);
                dot.position = new Vector3(
                    origin.x + vx * t,
                    Mathf.Max(y0 + vy * t - 0.5f * g * t * t, 2f),
                    origin.z + vz * t);
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 4f;
            line.endWidth = 4f;
            line.material = WebPortVisuals.CreateUnlit(WebPortVisuals.TextDark);
            line.gameObject.SetActive(false);
            return line;
        }

        private static void UpdateLine(LineRenderer line, int? targetId, PlayerState self, IReadOnlyDictionary<int, PlayerState> players)
        {
            if (!targetId.HasValue || !players.TryGetValue(targetId.Value, out PlayerState target))
            {
                line.gameObject.SetActive(false);
                return;
            }

            line.gameObject.SetActive(true);
            line.SetPosition(0, new Vector3(self.Position.x, 12f, self.Position.z));
            line.SetPosition(1, new Vector3(target.RenderPosition.x, 12f, target.RenderPosition.z));
        }
    }
}
