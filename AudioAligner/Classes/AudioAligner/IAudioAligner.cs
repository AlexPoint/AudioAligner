using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioAligner.Classes.AudioAligner
{
    public interface IAudioAligner
    {
        // Allow dynamic audio change
	    // returns true if change succeeded
	    bool setAudio(String pathToAudio);

	    // Allow dynamic transcription change
	    // returns false if change failed
	    bool setText(String text);
	
	    // Allow Deletions
	    bool allowDeletions();
	
	    // Allow Repetions
	    bool allowRepetions();
	
	    // Allow BackwardJumps
	    bool allowBackwardJumps();

	    // optimize values for aligner configuration
	    void optimize();

	    // align audio and return alignment result
	    string align();

	    void setAbsoluteBeamWidth(string absoluteBeamWidth);

	    void setRelativeBeamWidth(string relativeBeamWidth);
	
	    void setAddOutOfGrammarBranchProperty(string addOutOfGrammarBranch);

	    void setOutOfGrammarProbability(string outOfGrammarProbability);

	    void setPhoneInsertionProbability(string phoneInsertionProbability);

	    void setForwardJumpProbability(double prob);

	    void setBackwardJumpProbability(double prob);

	    void setSelfLoopProbability(double prob);
	
	    void setNumGrammarJumps(int n);
	
	    void performPhraseSpotting(bool doPhraseSpotting);
    }
}
