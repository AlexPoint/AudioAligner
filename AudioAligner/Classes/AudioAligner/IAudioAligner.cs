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
	    public boolean setAudio(String pathToAudio);

	    // Allow dynamic transcription change
	    // returns false if change failed
	    public boolean setText(String text) throws Exception;
	
	    // Allow Deletions
	    public boolean allowDeletions();
	
	    // Allow Repetions
	    public boolean allowRepetions();
	
	    // Allow BackwardJumps
	    public boolean allowBackwardJumps();

	    // optimize values for aligner configuration
	    public void optimize();

	    // align audio and return alignment result
	    public String align() throws Exception;

	    public void setAbsoluteBeamWidth(String absoluteBeamWidth);

	    public void setRelativeBeamWidth(String relativeBeamWidth);
	
	    public void setAddOutOfGrammarBranchProperty(String addOutOfGrammarBranch);

	    public void setOutOfGrammarProbability(String outOfGrammarProbability);

	    public void setPhoneInsertionProbability(String phoneInsertionProbability);

	    public void setForwardJumpProbability(double prob);

	    public void setBackwardJumpProbability(double prob);

	    public void setSelfLoopProbability(double prob);
	
	    public void setNumGrammarJumps(int n);
	
	    public void performPhraseSpotting(boolean doPhraseSpotting);
    }
}
