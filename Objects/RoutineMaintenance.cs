using JCass_ModelCore.Treatments;


namespace StarterModel.Objects;

public static class RoutineMaintenance
{

    public static TreatmentInstance GetRoutineMaintenance(RoadSegment segment, int period, Dictionary<string, Dictionary<string, object>> lookupSets)
    {
        // Note that maintenance cost calculation already checks if the segment is AC or Chipseal
        // and that the PDI is over the threshold specified for maintenance in lookups
        if (segment.MaintenanceCostPerKm <= 0) { return null!; }
     
        double cost = segment.MaintenanceCostPerKm * (segment.LengthInMetre / 1000);
        double quantity = cost / 1.0;  //Unit rate is 1.0
        string reason = "Routine Maintenance";
        string comment = $"PDI = {Math.Round(segment.PavementDistressIndex, 2)}; Rut = M{Math.Round(segment.RutParameterValue,2)}mm";

        string treatmentName = "RMaint";
        Dictionary<string, object> unitRateSet = lookupSets["unit_rate_set"];
        if (!unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(unitRateSet[treatmentName]);

        TreatmentInstance routMaint = new TreatmentInstance(segment.ElementIndex,treatmentName, period, quantity: quantity, 
                                                            unitRate: unitRate, false, reason, comment);
        return routMaint;

    }


}
