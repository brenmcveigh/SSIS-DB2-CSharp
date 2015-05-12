using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SSIS_DataFlow
{
    public partial class Popup : Form
    {
        public Popup()
        {
            InitializeComponent();
        }

        public void populatePopupCheckListbox(string str)
        {
            checkedListBox2.Items.Add(str);
        }

        public string GetSelectedCheckListBoxItem()
        {
            return checkedListBox2.SelectedItem.ToString();
        }


        internal void populatePopupCheckListbox()
        {
            throw new NotImplementedException();
        }

        private void checkedListBox2_ItemCheck(object sender, ItemCheckEventArgs e)
        {

            if (checkedListBox2.CheckedItems.Count == 1)
            {
                Boolean isCheckedItemBeingUnchecked = (e.CurrentValue == CheckState.Checked);
                if (isCheckedItemBeingUnchecked)
                {
                    e.NewValue = CheckState.Checked;
                }
                else
                {
                    Int32 checkedItemIndex = checkedListBox2.CheckedIndices[0];
                    checkedListBox2.ItemCheck -= checkedListBox2_ItemCheck;
                    checkedListBox2.SetItemChecked(checkedItemIndex, false);
                    checkedListBox2.ItemCheck += checkedListBox2_ItemCheck;
                }

                return;
            }


        }
    }
}
