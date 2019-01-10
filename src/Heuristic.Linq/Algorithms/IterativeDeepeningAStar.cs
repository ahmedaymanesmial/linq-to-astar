using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Heuristic.Linq.Algorithms
{
    static class IterativeDeepeningAStar
    {
        public static Node<TFactor, TStep> Run<TFactor, TStep>(HeuristicSearchBase<TFactor, TStep> source)
        {
            Debug.WriteLine("LINQ Expression Stack: {0}", source);

            return new IterativeDeepeningAStar<TFactor, TStep>(source).Run();
        }

        public static Node<TFactor, TStep> Run<TFactor, TStep>(HeuristicSearchBase<TFactor, TStep> source, IProgress<AlgorithmState<TFactor, TStep>> observer)
        {
            Debug.WriteLine("LINQ Expression Stack: {0}", source);

            return new ObservableIterativeDeepeningAStar<TFactor, TStep>(source, observer).Run();
        }
    }

    class IterativeDeepeningAStar<TFactor, TStep>
    {
        #region Fields

        private readonly HeuristicSearchBase<TFactor, TStep> _source;
        private readonly int _max = 1024;

        #endregion

        #region Properties

        public int MaxNumberOfLoops => _max;

        #endregion

        #region Constructor

        internal IterativeDeepeningAStar(HeuristicSearchBase<TFactor, TStep> source)
        {
            _source = source;
        }

        #endregion

        #region Public Methods

        public Node<TFactor, TStep> Run()
        {
            var counter = 0;
            var path = new Stack<Node<TFactor, TStep>>(_source.ConvertToNodes(_source.From, 0).OrderBy(n => n.Factor, _source.NodeComparer));
            var bound = path.Peek();

            while (counter <= _max)
            {
                var t = Search(path, bound, new HashSet<TStep>(_source.StepComparer));

                if (t.Flag == AlgorithmFlag.Found)
                    return t.Node;
                if (t.Flag == AlgorithmFlag.NotFound)
                    return null;

                // In Progress
                bound = t.Node;
                counter++;
            }
            return null;
        }

        #endregion

        #region Core

        private AlgorithmState<TFactor, TStep> Search(Stack<Node<TFactor, TStep>> path, Node<TFactor, TStep> bound, ISet<TStep> visited)
        {
            var current = path.Peek();

            if (_source.NodeComparer.Compare(current, bound) > 0)
                return new AlgorithmState<TFactor, TStep>(AlgorithmFlag.InProgress, current);

            if (_source.StepComparer.Equals(current.Step, _source.To))
                return new AlgorithmState<TFactor, TStep>(AlgorithmFlag.Found, current);

            var min = default(Node<TFactor, TStep>);
            var hasMin = false;
            var nexts = _source.Expands(current.Step, current.Level, visited.Add).ToArray();

            Array.Sort(nexts, _source.NodeComparer);

            foreach (var next in nexts)
            {
                Debug.WriteLine($"{current.Step}\t{current.Level} -> {next.Step}\t{next.Level}");

                next.Previous = current;
                path.Push(next);

                var t = Search(path, bound, visited);

                if (t.Flag == AlgorithmFlag.Found) return t;
                if (!hasMin || _source.NodeComparer.Compare(t.Node, min) < 0)
                {
                    min = t.Node;
                    hasMin = true;
                }
                path.Pop();
            }
            return new AlgorithmState<TFactor, TStep>(hasMin ? AlgorithmFlag.InProgress : AlgorithmFlag.NotFound, min);
        }

        #endregion 
    }

    class ObservableIterativeDeepeningAStar<TFactor, TStep>
    {
        #region Fields

        private readonly HeuristicSearchBase<TFactor, TStep> _source;
        private readonly IProgress<AlgorithmState<TFactor, TStep>> _observer;
        private readonly int _max = 1024;

        #endregion

        #region Properties

        public int MaxNumberOfLoops => _max;

        #endregion

        #region Constructor

        internal ObservableIterativeDeepeningAStar(HeuristicSearchBase<TFactor, TStep> source, IProgress<AlgorithmState<TFactor, TStep>> observer)
        {
            _source = source;
            _observer = observer;
        }

        #endregion

        #region Public Methods

        public Node<TFactor, TStep> Run()
        {
            var counter = 0;
            var path = new Stack<Node<TFactor, TStep>>(_source.ConvertToNodes(_source.From, 0).OrderBy(n => n.Factor, _source.NodeComparer));
            var bound = path.Peek();

            while (counter <= _max)
            {
                var t = Search(path, bound, new HashSet<TStep>(_source.StepComparer));

                if (t.Flag == AlgorithmFlag.Found)
                    return _observer.ReportAndReturn(t).Node;
                if (t.Flag == AlgorithmFlag.NotFound)
                    return _observer.NotFound();

                // In Progress
                bound = t.Node;
                counter++;
            }
            return _observer.NotFound();
        }

        #endregion

        #region Core

        private AlgorithmState<TFactor, TStep> Search(Stack<Node<TFactor, TStep>> path, Node<TFactor, TStep> bound, ISet<TStep> visited)
        {
            /*
             * Important Note: 
             * Only the status AlgorithmFlag.InProgress should be reported from this method
             * because either AlgorithmFlag.Found or AlgorithmFlag.NotFound should only occur once.
             */
            var current = path.Peek();

            if (_source.NodeComparer.Compare(current, bound) > 0)
                return _observer.ReportAndReturn(AlgorithmFlag.InProgress, current);

            if (_source.StepComparer.Equals(current.Step, _source.To))
                return new AlgorithmState<TFactor, TStep>(AlgorithmFlag.Found, current);

            var min = default(Node<TFactor, TStep>);
            var hasMin = false;
            var nexts = _source.Expands(current.Step, current.Level, visited.Add).ToArray();

            Array.Sort(nexts, _source.NodeComparer);

            foreach (var next in nexts)
            {
                Debug.WriteLine($"{current.Step}\t{current.Level} -> {next.Step}\t{next.Level}");

                next.Previous = current;
                path.Push(next);

                var t = Search(path, bound, visited);

                if (t.Flag == AlgorithmFlag.Found) return t;
                if (!hasMin || _source.NodeComparer.Compare(t.Node, min) < 0)
                {
                    min = t.Node;
                    hasMin = true;
                }
                path.Pop();
            }
            return hasMin ? _observer.ReportAndReturn(AlgorithmFlag.InProgress, current, path) : new AlgorithmState<TFactor, TStep>(AlgorithmFlag.NotFound, min, path);
        }

        #endregion 
    }


    internal struct IterativeDeepeningAStarAlgorithm : IAlgorithm, IObservableAlgorithm
    {
        string IAlgorithm.AlgorithmName => nameof(IterativeDeepeningAStar);

        Node<TFactor, TStep> IAlgorithm.Run<TFactor, TStep>(HeuristicSearchBase<TFactor, TStep> source)
        {
            Debug.WriteLine("LINQ Expression Stack: {0}", source);

            return new IterativeDeepeningAStar<TFactor, TStep>(source).Run();
        }

        Node<TFactor, TStep> IObservableAlgorithm.Run<TFactor, TStep>(HeuristicSearchBase<TFactor, TStep> source, IProgress<AlgorithmState<TFactor, TStep>> observer)
        {
            Debug.WriteLine("LINQ Expression Stack: {0}", source);

            return new ObservableIterativeDeepeningAStar<TFactor, TStep>(source, observer).Run();
        }
    }
}