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
        public static IntObj Attach(GameObject target, string hoverMessage, Vector3 colliderSize,
            UnityAction onInteract, System.Action<string> log, System.Action<string> warn)
        {
            var donor = FindDonor();
            if (donor == null) { warn("no InteractableObject donor found; prop will not be interactable"); return null; }

            var clone = Object.Instantiate(donor.gameObject, target.transform);
            clone.name = "Interactable";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // The donor's scale comes along with the clone. Left alone it multiplies the
            // collider below, giving the prop a hit box far larger than the visible wheel and
            // letting it catch the player's aim from well off target.
            clone.transform.localScale = Vector3.one;

            var io = clone.GetComponent<IntObj>();
            if (io == null) { warn("cloned object had no InteractableObject"); Object.Destroy(clone); return null; }

            LogComponents(clone, log);

            // RemoveAllListeners only clears runtime listeners. The donor's persistent
            // (editor-wired) listeners survive cloning and still target the ORIGINAL door, which
            // sits outside the cloned hierarchy -- so without disabling them, interacting with
            // this prop would also operate that door.
            MuteInherited(io.onInteractStart);
            MuteInherited(io.onInteractEnd);
            MuteInherited(io.onHovered);

            io.onInteractStart.AddListener(onInteract);
            io.SetMessage(hoverMessage);
            io.MaxInteractionRange = 3f;

            // The interaction raycast resolves an InteractableObject from the collider it hits,
            // searching upward. A collider on a sibling object is therefore never found, so the
            // collider has to live on this same GameObject.
            var col = clone.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.isTrigger = false;

            log($"   interactable at {clone.transform.position}, collider world bounds {col.bounds.size}");
            return io;
        }

        // Prefer the slot machine's own controls: they are purpose-built casino interactables, so
        // the prompt and feel match the machines beside the wheel rather than a door.
        private static IntObj FindDonor()
        {
            var slot = Object.FindObjectOfType<Il2CppScheduleOne.Casino.SlotMachine>();
            if (slot != null)
            {
                if (slot.HandleIntObj != null) return slot.HandleIntObj;
                if (slot.UpButton != null) return slot.UpButton;
            }

            return Object.FindObjectsOfType<Il2CppScheduleOne.Doors.StaticDoor>()
                .FirstOrDefault(d => d.IntObj != null)?.IntObj;
        }

        private static void MuteInherited(UnityEvent evt)
        {
            if (evt == null) return;
            evt.RemoveAllListeners();
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                evt.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        // GetType() on an Il2Cpp component reports "MonoBehaviour" rather than the real type, so
        // never identify components by that name -- an earlier version did and deleted the
        // InteractableObject it was trying to keep. GetIl2CppType() gives the true name.
        private static void LogComponents(GameObject clone, System.Action<string> log)
        {
            var names = clone.GetComponents<MonoBehaviour>()
                .Select(c => c.GetIl2CppType().Name);
            log($"   donor components: [{string.Join(", ", names)}]");
        }
    }
}
