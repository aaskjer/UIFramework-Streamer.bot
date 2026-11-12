using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace UIFramework
{
    /// <summary>
    /// Asks the game without holding the window still. A settings action answers through Streamer.bot's thread and
    /// waits for the game there, up to 35 seconds when the game's handler hangs; asked from the window's thread, that
    /// wait froze the window ("Not responding"). The question goes to another thread instead. An answer that is there
    /// in a moment is used at once, as it always was; one that takes longer arrives when it arrives.
    /// </summary>
    public static class Patient
    {
        /// <summary>What a settings action answers when the game did not (a timeout, a disabled or missing action),
        /// followed by ": " and why. Told apart from an answer that is empty.</summary>
        public const string NoAnswer = "\u0001uiframework:no-answer";

        /// <summary>How long the window's thread waits itself before it lets go, in milliseconds.</summary>
        public static int QuickMs = 200;

        public static bool IsNoAnswer(string answer) => answer != null && answer.StartsWith(NoAnswer, StringComparison.Ordinal);

        /// <summary>Why the game did not answer, from the marker's tail; "" when it says nothing.</summary>
        public static string Why(string answer) => IsNoAnswer(answer) ? answer.Substring(NoAnswer.Length).TrimStart(':', ' ') : "";

        /// <summary>Asks and goes on: <paramref name="then"/> gets the answer or what failed, on the calling thread. At
        /// once when the answer is quick; otherwise <paramref name="waiting"/> is called and this returns, and
        /// <paramref name="then"/> follows when the answer is there.</summary>
        public static void Ask<T>(Func<T> call, Action<T, Exception> then, Action waiting = null)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var task = Task.Run(call);
            if (Quick(task)) { Deliver(task, then); return; }
            waiting?.Invoke();
            task.ContinueWith(done => dispatcher.BeginInvoke(new Action(() => Deliver(done, then))));
        }

        /// <summary>Asks and waits for the answer, with the window alive: it paints, moves and answers Windows while
        /// <paramref name="busy"/> is told to keep its input away (true, then false). For what cannot go on without
        /// the answer: the check before a save.</summary>
        /// <exception cref="OperationCanceledException">The window's thread ended before the answer came.</exception>
        public static T Wait<T>(Func<T> call, Action<bool> busy = null)
        {
            var task = Task.Run(call);
            if (!Quick(task))
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var frame = new DispatcherFrame();
                task.ContinueWith(done => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
                busy?.Invoke(true);
                try { Dispatcher.PushFrame(frame); }
                finally { busy?.Invoke(false); }
                if (!task.IsCompleted)
                    throw new OperationCanceledException("The window closed while the game was asked.");
            }
            if (task.IsFaulted) throw Cause(task);
            return task.Result;
        }

        private static bool Quick(Task task)
        {
            try { return task.Wait(QuickMs); }
            catch (AggregateException) { return true; }   // it failed, quickly
        }

        private static void Deliver<T>(Task<T> task, Action<T, Exception> then)
        {
            if (task.IsFaulted) then(default(T), Cause(task));
            else then(task.Result, null);
        }

        // What the game's side threw, without the wrappers of the task and of the reflection call.
        private static Exception Cause(Task task)
        {
            Exception cause = task.Exception?.InnerException ?? task.Exception;
            while (cause is TargetInvocationException && cause.InnerException != null) cause = cause.InnerException;
            return cause ?? new InvalidOperationException("The game could not be asked.");
        }
    }
}
