using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using edu.cmu.sphinx.util.props;

namespace AudioAligner.Classes.PhraseSpotter
{
    abstract class PraseSpotter : Configurable
    {
        /**
	     * Hopefully there will be things here that will need configuration
	     */
	
	    public const string PROP_RECOGNIZER = "recognizer";
	    public const string PROP_GRAMMAR = "grammar";
	    public const string PROP_LINGUIST = "linguist";
	    public const string PROP_AUDIO_DATA_SOURCE = "audioFileDataSource";
	
	    public abstract void deallocate();
	
	    public abstract void startSpotting();
	
	    public abstract void setPhrase(string phrase);
	
	    public abstract List<PhraseSpotterResult> getTimedResult();
	
	    public abstract void setAudioDataSource(Uri audioFile);

        public abstract void newProperties(PropertySheet ps);
    }
}
