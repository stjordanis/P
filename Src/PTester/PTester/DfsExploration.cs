﻿using System;
using System.Collections.Generic;
using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using P.Runtime;
using System.Diagnostics;

namespace P.Tester
{
    static class DfsExploration
    {
#if DEBUG
        static int max_queue_size;
#endif
        static bool UseDepthBounding = false;
        static int DepthBound = 100;

        public static bool UseStateHashing = true; // currently doesn't make sense without

        public static bool FileDump = false;

        static HashSet<int> concretes      = new HashSet<int>();
        static HashSet<int> abstracts      = new HashSet<int>();
        static HashSet<int> abstract_succs = new HashSet<int>();

        static int size_concretes_previous = 0;
        static int size_abstracts_previous = 0;
        static int size_abstracts_previous_previous = 0;

        static HashSet<int> competitors;

        public static StateImpl start = null;

        public static void Dfs(bool queue_abstraction = false)
        {
            if (!UseStateHashing) throw new NotImplementedException();

            int k = PrtEventBuffer.k; // const ref would be better
            Console.WriteLine("Using " + ( k == 0 ? "unbounded queue" : "queue bound of " + k.ToString() ));

            concretes.Clear();
            abstracts.Clear();
            abstract_succs.Clear();

#if DEBUG
            max_queue_size = 0;
#endif

            var stack = new Stack<BacktrackingState>();

            StreamWriter concretes_SW      = null; if (FileDump) concretes_SW      = new StreamWriter("concretes-"      + (k < 10 ? "0" : "") + k.ToString() + ".txt");
            StreamWriter abstracts_SW      = null; if (FileDump) abstracts_SW      = new StreamWriter("abstracts-"      + (k < 10 ? "0" : "") + k.ToString() + ".txt");
            StreamWriter abstract_succs_SW = null; if (FileDump) abstract_succs_SW = new StreamWriter("abstract_succs-" + (k < 10 ? "0" : "") + k.ToString() + ".txt");

            StateImpl start_c = (StateImpl)start.Clone(); // we need a fresh clone in each iteration (k) of Dfs

            stack.Push(new BacktrackingState(start_c));
            int start_hash = start_c.GetHashCode();
            concretes.Add(start_hash);
            if (FileDump)
            {
                concretes_SW.Write(start_c.ToPrettyString()); // + " = " + start_hash.ToPrettyString());
                concretes_SW.WriteLine("==================================================");
            }

            if (queue_abstraction)
            {
                StateImpl start_c_ab = (StateImpl)start.Clone(); start_c_ab.abstract_me();
                abstracts.Add(start_c_ab.GetHashCode());
                start_c_ab.collect_abstract_successors(abstract_succs, abstract_succs_SW);
                if (FileDump)
                {

                    abstracts_SW.Write(start_c_ab.ToPrettyString());
                    abstracts_SW.WriteLine("==================================================");
                }
            }

            // DFS begin
            while (stack.Count != 0)
            {
                var bstate = stack.Pop();

                // PrintStackDepth(stack.Count);

                if (bstate.CurrIndex >= bstate.State.EnabledMachines.Count) // if "done" with bstate
                {
                    continue;
                }

                BacktrackingState next = Execute(bstate);    // execute the enabled machine pointed to by currIndex. Also, advance currIndex and/or choiceIndex
                stack.Push(bstate);                          // after increasing currIndex/choiceIndex, push state back on. This is like modifying bstate "on the stack"

                if (StateImpl.predHash != 0) // if we are in predecessor finding mode
                    check_pred_hash(bstate.State, next.State);

                if (! next.State.CheckFailure(next.depth))   // check for failure before adding new state: may fail due to failed assume, in which case we don't want to add
                {
                    // update concrete state hashset
                    var hash = next.State.GetHashCode();
                    if (!concretes.Add(hash))
                        continue;

                    stack.Push(next);

                    if (FileDump)
                    {
                        concretes_SW.Write(next.State.ToPrettyString()); // + " = " + hash.ToPrettyString());
                        concretes_SW.WriteLine("==================================================");
                    }

                    if (queue_abstraction)
                    {
                        StateImpl next_ab_s = (StateImpl)next.State.Clone(); next_ab_s.abstract_me();
                        if (abstracts.Add(next_ab_s.GetHashCode()))
                            if (FileDump)
                            {
                                abstracts_SW.Write(next_ab_s.ToPrettyString());
                                abstracts_SW.WriteLine("==================================================");
                            }

                        next_ab_s.collect_abstract_successors(abstract_succs, abstract_succs_SW);
                    }

#if DEBUG
                    // diagnostics

                    // Print number of states explored
                    if (concretes.Count % 1000 == 0)
                    {
                        Console.WriteLine("-------------- Number of concrete states visited so far   = {0}", concretes.Count);
                        Console.WriteLine("-------------- Number of abstract states found so far     = {0}", abstracts.Count);
                        Console.WriteLine("-------------- Number of abstract successors found so far = {0} (only those satisfying all static invariants)", abstract_succs.Count);
                    }

                    // update maximum encountered queue size
                    foreach (PrtImplMachine m in next.State.ImplMachines)
                    {
                        int m_size = m.eventQueue.Size();
                        if (m_size > max_queue_size)
                        {
                            max_queue_size = m_size;
                            Console.WriteLine("-------------- New maximum queue size observed  = {0}", max_queue_size);
                        }
                    }
#endif
                }
            }

            Console.WriteLine("");

            Console.WriteLine("Number of concrete states visited   = {0}", concretes.Count);
            Console.WriteLine("Number of abstract states found     = {0}", abstracts.Count);
            Console.WriteLine("Number of abstract successors found = {0} (only those satisfying all static invariants)", abstract_succs.Count);

            Console.WriteLine();

            if (FileDump)
            {
                concretes_SW.Close();
                abstracts_SW.Close();
                abstract_succs_SW.Close();
            }
        }

        public static void OS_Iterate()
        {
            if (PrtEventBuffer.k == 0) { Console.WriteLine("OS Exploration: skipping k=0 (makes no sense)"); goto Next_Round; }

            Console.Write("About to explore state space for queue bound k = {0}. Press <ENTER> to continue, anything else to 'Exit(0)': ", PrtEventBuffer.k);
            bool stop = ( Console.ReadKey().Key != ConsoleKey.Enter ); Console.WriteLine();
            if (stop)
            {
                Console.WriteLine("Exiting.");
                Environment.Exit(0);
            }

            try { Dfs(true); Debug.Assert(StateImpl.succHash == 0, "Dfs should always find the successor state with the given hash code"); }
            catch (StateImpl.SuccessorFound)
            {
                Console.WriteLine("Restarting Dfs ...");
                competitors = new HashSet<int>();
                Dfs();
                Console.WriteLine("Exiting.");
                Environment.Exit(0);
            }

            if (size_concretes_previous == concretes.Count)
            {
                Console.WriteLine("Global state sequence converged!");
                Console.Write("For fun, do you want to run the abstract convergence test as well? Press <ENTER> to continue, anything else to 'Exit(0)': ");
                stop = ( Console.ReadKey().Key != ConsoleKey.Enter ); Console.WriteLine();
                if (stop)
                {
                    Console.WriteLine("Exiting.");
                    Environment.Exit(0);
                }
            }

            // when do we have to run the abstract convergence test?
            if (size_abstracts_previous_previous < size_abstracts_previous && size_abstracts_previous == abstracts.Count)
            {
                Console.WriteLine("New plateau detected.");
                Console.Write("Running abstract state convergence test with tail-" + PrtEventBuffer.qt.ToString() + " abstraction ... ");

                if (abstract_converged())
                {
                    Console.WriteLine("converged!");
                    Environment.Exit(0);
                }
            }

            size_abstracts_previous_previous = size_abstracts_previous;
            size_abstracts_previous = abstracts.Count;
            size_concretes_previous = concretes.Count;

        Next_Round:
            ++PrtEventBuffer.k;
            OS_Iterate();
        }

        static void check_pred_hash(StateImpl s, StateImpl sp)
        {
            StateImpl a = (StateImpl)s.Clone(); a.abstract_me();
            if (a.GetHashCode() == StateImpl.predHash)
            {
                StateImpl bp = (StateImpl)sp.Clone(); bp.abstract_me();
                int bp_hash = bp.GetHashCode();
                if (competitors.Add(bp_hash))
                {
                    Console.WriteLine("Found new concrete pair (s,s') such that alpha(s) = a.");
                    string bp_f = "bp" + (competitors.Count - 1).ToString() + ".txt";
                    var bp_SW = new StreamWriter(bp_f);
                    bp_SW.WriteLine(bp.ToPrettyString());
                    bp_SW.Close();
                    Console.WriteLine("Pretty-printed b' = alpha(s') into file '{0}'. This abstract state is a \"competitor\" to candidate abstract successor a' .", bp_f);
                }
            }
        }

        // compute abstract successors, and return true ("converged") iff all of them are already contained in abstracts:
        static bool abstract_converged()
        {
            foreach (int hash in abstract_succs)
            {
                if (!abstracts.Contains(hash))
                {
                    Console.WriteLine("did not converge.");
                    Console.WriteLine("Found a so-far unreached abstract successor state. Its hash code is {0}.", hash);
                    Console.WriteLine("Do you want to");
                    Console.WriteLine("(c)ontinue, by increasing the queue bound, ignoring the unreached successor, OR");
                    Console.WriteLine("(i)nvestigate; we will then locate the state with that hash code.");
                    string answer;
                    do
                    {
                        Console.Write(" ? ");
                        answer = Console.ReadKey().Key.ToString().ToLower(); Console.WriteLine();
                    } while (answer != "c" && answer != "i");

                    if (answer == "c")
                        return false;

                    // for the re-run, queue abstraction type remains, k bound remains; file dumping is turned off
                    StateImpl.succHash = hash;
                    StateImpl.FileDump = false;
                    FileDump = false;
                    OS_Iterate();
                    Debug.Assert(false, "OS_Iterate should always find the successor state with the given hash code");
                }
            }
            return true;
        }

        static void PrintStackDepth(int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                Console.Write(".");
            }
            Console.WriteLine();
        }


        /// <summary>
        /// - runs the state machine pointed to by CurrIndex, in place, and returns the successor wrapped into a bstate
        /// - assigns to argument a clone (!) of the old bstate, and advances its choice vector and currIndex, as appropriate
        /// So bstate points to new memory after calling Execute. The returned successor is stored in old memory.
        /// </summary>
        /// <param name="bstate"></param>
        /// <returns></returns>
        static BacktrackingState Execute(BacktrackingState bstate)
        {
            var origState = (StateImpl)bstate.State.Clone();

            int choiceIndex = 0;

            // bstate.State.UserBooleanChoice is a pointer to a function with signature f: {} -> Bool.
            // The following assigns the code under 'delegate' to this function pointer.
            // bstate and choiceIndex are global variables
            bstate.State.UserBooleanChoice = delegate ()
            {
                if (choiceIndex < bstate.ChoiceVector.Count)
                {
                    return bstate.ChoiceVector[choiceIndex++];
                }

                choiceIndex++;
                bstate.ChoiceVector.Add(false);
                return false;
            };

            bstate.State.EnabledMachines[bstate.CurrIndex].PrtRunStateMachine();

            Debug.Assert(choiceIndex == bstate.ChoiceVector.Count);

            // flip last choice

            // remove all 1's from the right
            while (bstate.ChoiceVector.Count > 0 && bstate.ChoiceVector[bstate.ChoiceVector.Count - 1])
            {
                bstate.ChoiceVector.RemoveAt(bstate.ChoiceVector.Count - 1);
            }

            if (bstate.ChoiceVector.Count > 0)
            {
                bstate.ChoiceVector[bstate.ChoiceVector.Count - 1] = true;
            }

            var ret = new BacktrackingState(bstate.State);
            ret.depth = bstate.depth + 1;

            bstate.State = origState; // a clone of bstate.State

            if (bstate.ChoiceVector.Count == 0)
            {
                bstate.CurrIndex++; // first iterate through all choices. When exhausted, step to the next enabled machine
            }

            return ret;
        }
    }

    class BacktrackingState
    {
        public StateImpl State;
        public int CurrIndex;            // index of the next machine to execute
        public List<bool> ChoiceVector;  // length = number of choices to be made; contents of list = current choice as bitvector
        public int depth;                // used only with depth bounding

        public BacktrackingState(StateImpl state)
        {
            this.State = state;
            CurrIndex = 0;
            ChoiceVector = new List<bool>();
            depth = 0;
        }

    }
}