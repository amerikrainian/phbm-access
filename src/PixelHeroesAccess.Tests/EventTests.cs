using System.Collections.Generic;
using PixelHeroesAccess.Contracts;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Buffers;
using PixelHeroesAccess.Core.Events;
using Xunit;

namespace PixelHeroesAccess.Tests
{
    public class EventTests
    {
        private sealed class Store : ISettingsStore
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public bool GetBool(string key, bool defaultValue) => Values.TryGetValue(key, out var v) ? (bool)v : defaultValue;
            public void SetBool(string key, bool value) => Values[key] = value;
            public int GetInt(string key, int defaultValue) => defaultValue;
            public void SetInt(string key, int value) { }
            public string GetString(string key, string defaultValue) => defaultValue;
            public void SetString(string key, string value) { }
        }

        [Fact]
        public void TheLogIsNewestFirstWithAMultiLineEventInItsOwnOrder()
        {
            var log = new EventLog();
            log.Add("Turn 1.");
            log.Add("first\nsecond");
            Assert.Equal(new[] { "first", "second", "Turn 1." }, log.Lines);
        }

        [Fact]
        public void AReviewingCursorKeepsItsLineAsEventsArrive()
        {
            var log = new EventLog();
            var buffer = new Buffer("events", () => "events");
            buffer.SetSource(() => log.Lines);
            log.Added += buffer.LinesInserted;
            log.Add("a");
            log.Add("b");
            log.Add("c");
            buffer.MoveNext();
            Assert.Equal("b", buffer.CurrentLine);
            log.Add("d");
            Assert.Equal("b", buffer.CurrentLine);
            // On the top line, the cursor stays on top: the newest.
            buffer.MovePrevious();
            buffer.MovePrevious();
            Assert.Equal("d", buffer.CurrentLine);
            log.Add("e");
            Assert.Equal("e", buffer.CurrentLine);
        }

    }
}
