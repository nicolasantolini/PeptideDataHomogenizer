namespace PeptideDataHomogenizer.Data
{
    using System.Collections.Generic;
    using Entities.RegexData;
    using Microsoft.AspNetCore.Mvc;

    public class DatabaseInitializer
    {
        private readonly DatabaseDataHandler _dataHandler;

        public DatabaseInitializer([FromServices] DatabaseDataHandler dataHandler)
        {
            _dataHandler = dataHandler;
        }

        public async Task InitializeDatabase()
        {
            if(await _dataHandler.GetCountAsync<ForceFieldSoftware>()==0)
                await InitializeForceFields();
            if(await _dataHandler.GetCountAsync<Ion>()==0)
                await InitializeIons();
            if(await _dataHandler.GetCountAsync<SimulationMethod>()==0)
                await InitializeSimulationMethods();
            if(await _dataHandler.GetCountAsync<SimulationSoftware>()==0)
                await InitializeSimulationSoftware();
            if(await _dataHandler.GetCountAsync<WaterModel>()==0)
                await InitializeWaterModels();
        }

        private async Task InitializeForceFields()
        {
            var forceFields = new List<ForceFieldSoftware>
                {
                    new ForceFieldSoftware { SoftwareName = "AMBER" },
                    new ForceFieldSoftware { SoftwareName = "CHARMM" },
                    new ForceFieldSoftware { SoftwareName = "GROMOS" },
                    new ForceFieldSoftware { SoftwareName = "OPLS-AA" },
                    new ForceFieldSoftware { SoftwareName = "GAFF" },
                    new ForceFieldSoftware { SoftwareName = "CGenFF" },
                    new ForceFieldSoftware { SoftwareName = "COMPASS" },
                    new ForceFieldSoftware { SoftwareName = "MMFF" },
                    new ForceFieldSoftware { SoftwareName = "Drude Polarizable" },
                    new ForceFieldSoftware { SoftwareName = "AMOEBA" },
                    new ForceFieldSoftware { SoftwareName = "FF" },
                    new ForceFieldSoftware { SoftwareName = "Martini" },
                    new ForceFieldSoftware { SoftwareName = "OPLS" },
                    new ForceFieldSoftware { SoftwareName = "OPLS/AA" }
                };

            await _dataHandler.AddRangeAsync(forceFields);
        }

        private async Task InitializeIons()
        {
            var ions = new List<Ion>
                {
                    new Ion { IonName = "NaCl" },
                    new Ion { IonName = "KCl" },
                    new Ion { IonName = "LiTFSI" },
                    new Ion { IonName = "LiCl" },
                    new Ion { IonName = "CsCl" },
                    new Ion { IonName = "La(Tf2N)3" },
                    new Ion { IonName = "Dy(Tf2N)3" },
                    new Ion { IonName = "LiBF4" },
                    new Ion { IonName = "LiPF6" },
                    new Ion { IonName = "NaTf2N" },
                    new Ion { IonName = "KTf2N" },
                    new Ion { IonName = "LiF-LiBe2" }
                };

            await _dataHandler.AddRangeAsync(ions);
        }

        private async Task InitializeSimulationMethods()
        {
            var methods = new List<SimulationMethod>
                {
                    new SimulationMethod { MethodName = "Steered Molecular Dynamics" },
                    new SimulationMethod { MethodName = "Replica Exchange Molecular Dynamics" },
                    new SimulationMethod { MethodName = "Metadynamics" },
                    new SimulationMethod { MethodName = "Well-Tempered Metadynamics" },
                    new SimulationMethod { MethodName = "Umbrella Sampling" },
                    new SimulationMethod { MethodName = "Adaptive Biasing Force" },
                    new SimulationMethod { MethodName = "Transition Path Sampling" },
                    new SimulationMethod { MethodName = "Accelerated Molecular Dynamics" },
                    new SimulationMethod { MethodName = "Targeted Molecular Dynamics" },
                    new SimulationMethod { MethodName = "Langevin Dynamics" },
                    new SimulationMethod { MethodName = "Brownian Dynamics" },
                    new SimulationMethod { MethodName = "Dissipative Particle Dynamics" },
                    new SimulationMethod { MethodName = "Coarse-Grained Molecular Dynamics" },
                    new SimulationMethod { MethodName = "QM/MM Molecular Dynamics" },
                    new SimulationMethod { MethodName = "Free Energy Perturbation" },
                    new SimulationMethod { MethodName = "Thermodynamic Integration" },
                    new SimulationMethod { MethodName = "Monte Carlo" },
                    new SimulationMethod { MethodName = "Metropolis Monte Carlo" },
                    new SimulationMethod { MethodName = "Kinetic Monte Carlo simulation" }
                };

            await _dataHandler.AddRangeAsync(methods);
        }

        private async Task InitializeSimulationSoftware()
        {
            var software = new List<SimulationSoftware>
                {
                    new SimulationSoftware { SoftwareName = "Abalone" },
                    new SimulationSoftware { SoftwareName = "ADF" },
                    new SimulationSoftware { SoftwareName = "Ascalaph Designer" },
                    new SimulationSoftware { SoftwareName = "Avogadro" },
                    new SimulationSoftware { SoftwareName = "BOSS" },
                    new SimulationSoftware { SoftwareName = "Folding@home" },
                    new SimulationSoftware { SoftwareName = "CP2K" },
                    new SimulationSoftware { SoftwareName = "Desmond" },
                    new SimulationSoftware { SoftwareName = "Discovery Studio" },
                    new SimulationSoftware { SoftwareName = "Espresso" },
                    new SimulationSoftware { SoftwareName = "fold.it" },
                    new SimulationSoftware { SoftwareName = "FoldX" },
                    new SimulationSoftware { SoftwareName = "GROMACS" },
                    new SimulationSoftware { SoftwareName = "HOOMD-blue" },
                    new SimulationSoftware { SoftwareName = "Schroedinger" },
                    new SimulationSoftware { SoftwareName = "Schroedinger Suite" },
                    new SimulationSoftware { SoftwareName = "LAMMPS" },
                    new SimulationSoftware { SoftwareName = "MAPS" },
                    new SimulationSoftware { SoftwareName = "MDynaMix" },
                    new SimulationSoftware { SoftwareName = "MOE" },
                    new SimulationSoftware { SoftwareName = "ms2" },
                    new SimulationSoftware { SoftwareName = "OpenMM" },
                    new SimulationSoftware { SoftwareName = "Orac" },
                    new SimulationSoftware { SoftwareName = "NAMD" },
                    new SimulationSoftware { SoftwareName = "NWChem" },
                    new SimulationSoftware { SoftwareName = "PLUMED" },
                    new SimulationSoftware { SoftwareName = "SAMSON" },
                    new SimulationSoftware { SoftwareName = "Scigress" },
                    new SimulationSoftware { SoftwareName = "Spartan" },
                    new SimulationSoftware { SoftwareName = "TeraChem" },
                    new SimulationSoftware { SoftwareName = "TINKER" },
                    new SimulationSoftware { SoftwareName = "VASP" },
                    new SimulationSoftware { SoftwareName = "YASARA" },
                    new SimulationSoftware { SoftwareName = "AMBER" }
                };

            await _dataHandler.AddRangeAsync(software);
        }

        private async Task InitializeWaterModels()
        {
            var waterModels = new List<WaterModel>
                {
                    new WaterModel { WaterModelName = "Quantum Models", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "Coarse Grained Solvent", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "Nonlinear PB", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "Linear PB", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "PB/SASA", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "GB/SASA/VOL", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "SASA", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "GB", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "VOL", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "Distance-dependent dielectric", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "Martini", WaterModelType = "implicit" },
                    new WaterModel { WaterModelName = "TIPS", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "SPC", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP3P", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "SPC/E", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "BF", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIPS2", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP4P", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP4P-Ew", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP4P/Ice", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP4P/2005", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "OPC", WaterModelType = "explicit" },
                    new WaterModel { WaterModelName = "TIP4P-D", WaterModelType = "explicit" }
                };

            await _dataHandler.AddRangeAsync(waterModels);
        }
    }
}
