using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>An immutable river contribution to a shared Core planning snapshot.</summary>
    public sealed class RiverOffer
    {
        public PlanIdentity Identity { get; }
        public WaterCorridor Corridor { get; }
        public IReadOnlyList<CrossingCandidate> Crossings { get; }
        public IReadOnlyList<AreaReservation> Reservations { get; }

        /// <summary>Exports this river instance through Core without depending on other modules.</summary>
        public PlanContribution ToContribution(string moduleInstanceId)
        {
            return new PlanContribution(moduleInstanceId, Identity, Reservations, new[] { Corridor }, Crossings);
        }

        public RiverOffer(PlanIdentity identity, WaterCorridor corridor,
            IEnumerable<CrossingCandidate> crossings, IEnumerable<AreaReservation> reservations)
        {
            if (!identity.IsValid) throw new ArgumentException("Construct a valid plan identity.", nameof(identity));
            Corridor = corridor ?? throw new ArgumentNullException(nameof(corridor));
            Identity = identity;
            Crossings = CopyUnique(crossings, nameof(crossings), candidate => candidate.Id);
            Reservations = CopyUnique(reservations, nameof(reservations), reservation => reservation.Id);
            foreach (var candidate in Crossings)
            {
                if (!string.Equals(candidate.WaterId, Corridor.Id, StringComparison.Ordinal)
                    || !Corridor.Banks.Contains(candidate.Bounds)
                    || !Corridor.Bed.Overlaps(candidate.Bounds))
                    throw new ArgumentException("Crossing must reference this corridor and overlap its bed within its banks: " + candidate.Id, nameof(crossings));
            }
            foreach (var reservation in Reservations)
            {
                if (reservation.OwnerId != Corridor.Id
                    || (reservation.Purpose != ReservationPurpose.RiverBed && reservation.Purpose != ReservationPurpose.RiverBank
                        && reservation.Purpose != ReservationPurpose.ProtectedArea))
                    throw new ArgumentException("River reservations must belong to this corridor and describe its bed, bank or protected margin.", nameof(reservations));
                if (reservation.Purpose != ReservationPurpose.ProtectedArea
                    && !(reservation.Purpose == ReservationPurpose.RiverBed ? Corridor.Bed : Corridor.Banks).Contains(reservation.Bounds))
                    throw new ArgumentException("River reservation must fit its declared envelope: " + reservation.Id, nameof(reservations));
            }
        }

        private static ReadOnlyCollection<T> CopyUnique<T>(IEnumerable<T> source, string parameter, Func<T, string> id)
        {
            if (source == null) throw new ArgumentNullException(parameter);
            var copy = source.ToArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in copy)
            {
                if (item == null) throw new ArgumentException("Null offer.", parameter);
                if (!seen.Add(id(item))) throw new ArgumentException("Duplicate ID: " + id(item), parameter);
            }
            Array.Sort(copy, (left, right) => StringComparer.Ordinal.Compare(id(left), id(right)));
            return Array.AsReadOnly(copy);
        }
    }
}
