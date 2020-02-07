using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp
{
    public class JumpLabel
    {
        public string originalName;
        public string uniqueName;
        public int useCount = 0;

        public uint resolvedAddress = uint.MaxValue - 1;

        public bool IsResolved { get { return resolvedAddress != (uint.MaxValue - 1); } }

        public string AddresStr() { return string.Format("0x{0:X8}", resolvedAddress); }
    }

    public class LabelTable
    {
        private Dictionary<string, int> nameCounter;
        private List<JumpLabel> jumpLabels;

        public LabelTable()
        {
            nameCounter = new Dictionary<string, int>();
            jumpLabels = new List<JumpLabel>();
        }

        public JumpLabel GetNewJumpLabel(string labelName)
        {
            int labelCounter;
            if (!nameCounter.TryGetValue(labelName, out labelCounter))
            {
                labelCounter = 0;
                nameCounter.Add(labelName, labelCounter);
            }
            else
            {
                labelCounter = ++nameCounter[labelName];
            }

            JumpLabel newLabel = new JumpLabel();

            newLabel.originalName = labelName;
            newLabel.uniqueName = $"{labelName}_{labelCounter}";

            jumpLabels.Add(newLabel);

            return newLabel;
        }

        public JumpLabel GetLabel(string uniqueLabelName)
        {
            foreach (JumpLabel label in jumpLabels)
            {
                if (label.uniqueName == uniqueLabelName)
                    return label;
            }

            return null;
        }
    }

}
