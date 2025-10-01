// Profil_View.cs
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Digital_Farming.Functii;

namespace Digital_Farming
{
    public partial class Profil_View : Form
    {
        private readonly Profil _profile;

        private readonly List<string> _cultures = new() { "Tomatoes", "Cabbage", "Cucumber" };
        private readonly List<string> _substrates = new() { "Coco Coir", "Rockwool", "Peat Moss" };

        public Profil_View(Profil profile)
        {
            InitializeComponent();
            _profile = profile;

            cmbCultureType.DataSource = _cultures;
            cmbSubstrateType.DataSource = _substrates;

            txtContainerSize.Text = _profile.ContainerSizeL.ToString("0.##");
            txtPlantCount.Text = _profile.PlantCount.ToString();
            cmbCultureType.SelectedItem = _profile.Culture;
            cmbSubstrateType.SelectedItem = _profile.SubstrateType;
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            if (!float.TryParse(txtContainerSize.Text, out var sizeL))
            {
                MessageBox.Show("Container Size must be a number.",
                                "Validation Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                txtContainerSize.Focus();
                return;
            }

            if (!int.TryParse(txtPlantCount.Text, out var plantCount))
            {
                MessageBox.Show("Plant Count must be an integer.",
                                "Validation Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                txtPlantCount.Focus();
                return;
            }

            _profile.ContainerSizeL = sizeL;
            _profile.PlantCount = plantCount;
            _profile.Culture = cmbCultureType.SelectedItem as string;
            _profile.SubstrateType = cmbSubstrateType.SelectedItem as string;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
