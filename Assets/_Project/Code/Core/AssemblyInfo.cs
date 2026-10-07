using System.Runtime.CompilerServices;

// SaveService's two test seams are internal: Open (read a given file and write there from then on) and
// SavePath (the file it is on). With them an EditMode test points a real service at a temp save, and the
// PlayMode guard checks that the live service is on the run's own save before it drives a single write.
// Tests only; nothing else should bind these internals.
[assembly: InternalsVisibleTo("HiddenHarbours.Tests.EditMode")]
[assembly: InternalsVisibleTo("HiddenHarbours.Tests.PlayMode")]
