using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelHeroesAccess.Dev
{
    /// <summary>
    /// Raw structural dump of the live scene for the dev /gui endpoint. The game draws everything with
    /// 2D Toolkit (no uGUI, no TextMesh Pro): every active GameObject of every loaded scene (and the
    /// DontDestroyOnLoad set, reached through a root's scene), indented by depth, with its component
    /// types, a <c>tk2dTextMesh</c>'s text, a <c>tk2dSprite</c>'s sprite name, a <c>tk2dButton</c>'s
    /// enabled and triggerable state, and the world position (the game lays its screens out by
    /// position, and the gamepad's fallback navigation walks positions). Reads live state; caches nothing.
    /// </summary>
    internal static class GuiInspector
    {
        public static string Describe()
        {
            var sb = new StringBuilder();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                sb.Append("=== scene: ").Append(scene.name).Append(" ===\n");
                foreach (var root in scene.GetRootGameObjects())
                    Walk(root.transform, 0, sb);
            }
            return sb.ToString();
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            GameObject go = t.gameObject;
            if (!go.activeInHierarchy)
                return;

            sb.Append(' ', depth * 2).Append(go.name);
            AppendComponents(go, sb);
            Vector3 p = t.position;
            sb.Append("  @").Append(p.x.ToString("0.#")).Append(',').Append(p.y.ToString("0.#")).Append(',').Append(p.z.ToString("0.#"));

            var text = go.GetComponent<tk2dTextMesh>();
            if (text != null && !string.IsNullOrEmpty(text.text))
                sb.Append("  \"").Append(text.text.Replace("\n", "\\n")).Append('"');
            var sprite = go.GetComponent<tk2dBaseSprite>();
            if (sprite != null && sprite.Collection != null && sprite.CurrentSprite != null)
                sb.Append("  sprite=").Append(sprite.CurrentSprite.name);
            var button = go.GetComponent<tk2dButton>();
            if (button != null)
                sb.Append("  button(enabled=").Append(button.enabled)
                  .Append(", triggerable=").Append(SafeTriggerable(button))
                  .Append(", target=").Append(button.HasTriggerTarget()).Append(')');

            sb.Append('\n');

            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }

        private static string SafeTriggerable(tk2dButton button)
        {
            try { return button.IsTriggerable().ToString(); }
            catch (System.Exception e) { return "error " + e.GetType().Name; }
        }

        private static void AppendComponents(GameObject go, StringBuilder sb)
        {
            bool any = false;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                    continue; // a missing-script slot
                string name = c.GetType().Name;
                if (name == "Transform" || name == "MeshFilter" || name == "MeshRenderer" || name == "BoxCollider")
                    continue;
                sb.Append(any ? ", " : "  [").Append(name);
                any = true;
            }
            if (any)
                sb.Append(']');
        }
    }
}
