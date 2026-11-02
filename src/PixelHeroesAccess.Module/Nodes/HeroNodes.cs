using System;
using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Nodes
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// Node factories for heroes and items. A hero node carries the hero buffer and the items buffer
    /// (<see cref="Buffers.HeroKey"/>, <see cref="Buffers.ItemsKey"/>) through its side lines; an item
    /// node carries its facts as details (the control buffer) and, when a hero holds it, that hero's
    /// buffers too, so the review keys reach the whole hero from any of their items.
    /// </summary>
    public static class HeroNodes
    {
        /// <summary>The side lines of a hero: the hero buffer and the items buffer.</summary>
        public static Func<string, IEnumerable<string>> Sides(Func<CharacterHero> hero, bool tavern = false)
            => key =>
            {
                var h = hero();
                if (h == null) return null;
                if (key == Buffers.HeroKey) return HeroLines.Lines(h, tavern);
                if (key == Buffers.ItemsKey) return HeroLines.Items(h);
                return null;
            };

        /// <summary>A hero as a control: the label given, the hero's buffers, Enter runs the activation.</summary>
        public static NodeVtable Hero(Func<CharacterHero> hero, Func<string> label, Action activate = null,
            Func<bool> enabled = null, bool tavern = false)
        {
            var vt = GameNodes.Button(label, activate, enabled, activate == null ? ControlTypes.Text : ControlTypes.Button);
            vt.SideLines = Sides(hero, tavern);
            vt.Subject = () => hero();
            return vt;
        }

        /// <summary>An item as a control: its summary as the label (or the label given), its facts in
        /// the control buffer, its holder's buffers when one is given.</summary>
        public static NodeVtable Item(Func<Item> item, Func<CharacterHero> holder, Action activate = null,
            Func<string> label = null, Func<bool> enabled = null)
        {
            Func<string> lbl = label ?? (() => ItemLines.Summary(item(), holder != null ? holder() : null));
            var vt = GameNodes.Button(lbl, activate, enabled, activate == null ? ControlTypes.Text : ControlTypes.Button);
            vt.Details = () => ItemLines.Lines(item(), holder != null ? holder() : null);
            if (holder != null) vt.SideLines = Sides(holder);
            return vt;
        }
    }
}
