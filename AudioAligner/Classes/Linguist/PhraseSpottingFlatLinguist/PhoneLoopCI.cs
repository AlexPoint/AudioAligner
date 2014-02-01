using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.sun.org.apache.regexp.@internal;
using edu.cmu.sphinx.linguist;
using edu.cmu.sphinx.linguist.acoustic;
using edu.cmu.sphinx.linguist.dictionary;
using edu.cmu.sphinx.linguist.flat;
using edu.cmu.sphinx.util;

namespace AudioAligner.Classes.Linguist.PhraseSpottingFlatLinguist
{
    /**
     * Constructs a loop of all the context-independent phones. This loop is used in the static flat linguist for detecting
     * out-of-grammar utterances. A 'phoneInsertionProbability' will be added to the score each time a new phone is entered.
     * To obtain the all-phone search graph loop, simply called the method {@link #getSearchGraph() getSearchGraph}.
     * <p/>
     * For futher details of this approach cf. 'Modeling Out-of-vocabulary Words for Robust Speech Recognition', Brazzi,
     * 2000, Proc. ICSLP
     */
    class PhoneLoopCI
    {
        public readonly AcousticModel model;
        private readonly float logPhoneInsertionProbability;
        public static readonly float logOne = LogMath.getLogOne();
        public SentenceHMMState inititalState;


        /**
         * Creates the CIPhoneLoop with the given acoustic model and phone insertion probability
         *
         * @param model                        the acoustic model
         * @param logPhoneInsertionProbability the insertion probability
         */
        public PhoneLoopCI(AcousticModel model,
                           float logPhoneInsertionProbability,
                           SentenceHMMState initialState) {
            this.model = model;
            this.logPhoneInsertionProbability =
                    logPhoneInsertionProbability;
            this.inititalState = initialState;
        }


        /**
         * Creates a new loop of all the context-independent phones.
         *
         * @return the phone loop search graph
         */
        public SearchGraph getSearchGraph() {
            return new PhoneLoopSearchGraph(this.inititalState, this.model, this.logPhoneInsertionProbability);
        }
        
    }

    class PhoneLoopSearchGraph : SearchGraph
    {
        public static readonly float logOne = LogMath.getLogOne();
        public readonly AcousticModel model;
        protected readonly Dictionary<String, SearchState> existingStates;
        private readonly float logPhoneInsertionProbability;

        protected readonly SentenceHMMState firstState;
        protected readonly SentenceHMMState inititalState;


        /** Constructs a phone loop search graph. */
        public PhoneLoopSearchGraph(SentenceHMMState initState, AcousticModel model, float logPhoneInsertionProbability)
        {
            this.inititalState = initState;
            this.model = model;
            this.logPhoneInsertionProbability = logPhoneInsertionProbability;
            existingStates = new Dictionary<string, SearchState>();
            firstState = new UnknownWordState();
            SentenceHMMState branchState = new BranchOutState(firstState);
            attachState(firstState, branchState, logOne, logOne);

            SentenceHMMState lastState = new LoopBackState(firstState);
            //lastState.setFinalState(true);
            //attachState(lastState, branchState, LogMath.getLogZero(),
            //		LogMath.getLogZero());
            attachState(lastState, inititalState, logOne, logOne);

            for (java.util.Iterator i = model.getContextIndependentUnitIterator(); i.hasNext(); )
            {
                Unit unit = (Unit) i.next();
                UnitState unitState = new UnitState(unit, HMMPosition.UNDEFINED);

                // attach unit state to the branch out state
                attachState(branchState, unitState, logOne, logPhoneInsertionProbability);

                HMM hmm = model.lookupNearestHMM
                        (unitState.getUnit(), unitState.getPosition(), false);
                HMMState initialState = hmm.getInitialState();
                HMMStateState hmmTree = new HMMStateState(unitState, initialState);
                addStateToCache(hmmTree);

                // attach first HMM state to the unit state
                attachState(unitState, hmmTree, logOne, logOne);

                // expand the HMM tree
                HMMStateState finalState = expandHMMTree(unitState, hmmTree);

                // attach final state of HMM tree to the loopback state
                attachState(finalState, lastState, logOne, logOne);
            }
        }


        /**
         * Retrieves initial search state
         *
         * @return the set of initial search state
         */
        //@Override
        public SearchState getInitialState()
        {
            return firstState;
        }


        /**
         * Returns the number of different state types maintained in the search graph
         *
         * @return the number of different state types
         */
        //@Override
        public int getNumStateOrder()
        {
            return 5;
        }


        /**
         * Checks to see if a state that matches the given state already exists
         *
         * @param state the state to check
         * @return true if a state with an identical signature already exists.
         */
        private SentenceHMMState getExistingState(SentenceHMMState state)
        {
            return (SentenceHMMState)existingStates[state.getSignature()];
        }


        /**
         * Adds the given state to the cache of states
         *
         * @param state the state to add
         */
        protected void addStateToCache(SentenceHMMState state)
        {
            existingStates.Add(state.getSignature(), state);
        }


        /**
         * Expands the given hmm state tree
         *package edu.cmu.sphinx.linguist.KWSFlatLinguist;

public class PhoneLoopCI {

}

         * @param parent the parent of the tree
         * @param tree   the tree to expand
         * @return the final state in the tree
         */
        protected HMMStateState expandHMMTree(UnitState parent,
                                            HMMStateState tree)
        {
            HMMStateState retState = tree;
            foreach (HMMStateArc arc in tree.getHMMState().getSuccessors())
            {
                HMMStateState newState;
                if (arc.getHMMState().isEmitting())
                {
                    newState = new HMMStateState
                        (parent, arc.getHMMState());
                }
                else
                {
                    newState = new NonEmittingHMMState
                        (parent, arc.getHMMState());
                }
                SentenceHMMState existingState = getExistingState(newState);
                float logProb = arc.getLogProbability();
                if (existingState != null)
                {
                    attachState(tree, existingState, logOne, logProb);
                }
                else
                {
                    attachState(tree, newState, logOne, logProb);
                    addStateToCache(newState);
                    retState = expandHMMTree(parent, newState);
                }
            }
            return retState;
        }


        protected void attachState(SentenceHMMState prevState,
                                   SentenceHMMState nextState,
                                   float logLanguageProbability,
                                   float logInsertionProbability)
        {
            SentenceHMMStateArc arc = new SentenceHMMStateArc
                    (nextState,
                     logLanguageProbability,
                     logInsertionProbability);
            prevState.connect(arc);
        }
    }

    class UnknownWordState : SentenceHMMState, WordSearchState {

        //@Override
        public Pronunciation getPronunciation() {
            return Word.UNKNOWN.getPronunciations()[0];
        }


        //@Override
        public override int getOrder() {
            return 0;
        }


        //@Override
        public override string getName() {
            return "UnknownWordState";
        }


        /**
         * Returns true if this UnknownWordState indicates the start of a word. Returns false if this UnknownWordState
         * indicates the end of a word.
         *
         * @return true if this UnknownWordState indicates the start of a word, false if this UnknownWordState indicates the
         *         end of a word
         */
        //@Override
        public override bool isWordStart() {
            return true;
        }
    }

    class LoopBackState : SentenceHMMState {

        public LoopBackState(SentenceHMMState parent): base("CIPhonesLoopBackState", parent, 0){}


        //@Override
        public override int getOrder() {
            return 1;
        }
    }

    class BranchOutState : SentenceHMMState {

        public BranchOutState(SentenceHMMState parent): base("BranchOutState", parent, 0){}


        //@Override
        public override int getOrder() {
            return 1;
        }
    }
}
