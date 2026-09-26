using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using IntObj = Il2CppScheduleOne.Interaction.InteractableObject;

namespace CasinoExpansion.World
{
    // InteractableObject carries editor-serialized asset references (_currentBindingData,
    // _descriptorData) that ShowMessage/SetInputData consume. AddComponent leaves those null and
    // null-refs on hover, so the only safe way to get one at runtime is to clone a live instance.
    public static class InteractableFactory
    {
        public static IntObj Attach(GameObject target, string hoverMessage,
            UnityAction onInteract, System.Action<string> log, System.Action<string> warn)
        {
            var donor = Object.FindObjectsOfType<Il2CppScheduleOne.Doors.StaticDoor>()
                .FirstOrDefault(d => d.IntObj != null)?.IntObj;

            if (donor == null) { warn("no InteractableObject donor found; prop will not be interactable"); return null; }

            var clone = Object.Instantiate(donor.gameObject, target.transform);
            clone.name = "Interactable";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // The donor's own scripts and event wiring come along with the clone -- without
            // stripping them, clicking the wheel would also try to open a door.
            StripDonorBehaviour(clone, log);

            var io = clone.GetComponent<IntObj>();
            if (io == null) { warn("cloned object had no InteractableObject"); Object.Destroy(clone); return null; }

            io.onInteractStart.RemoveAllListeners();
            io.onInteractEnd.RemoveAllListeners();
            io.onHovered.RemoveAllListeners();

            io.onInteractStart.AddListener(onInteract);
            io.SetMessage(hoverMessage);
            io.MaxInteractionRange = 3f;

            return io;
        }

        private static void StripDonorBehaviour(GameObject clone, System.Action<string> log)
        {
            var kept = "";
            foreach (var c in clone.GetComponents<MonoBehaviour>().ToArray())
            {
                var name = c.GetType().Name;
                if (name == nameof(IntObj)) { kept += name + " "; continue; }

                Object.Destroy(c);
                log($"   stripped donor component: {name}");
            }
            log($"   kept: {kept.Trim()}");
        }
    }
}
