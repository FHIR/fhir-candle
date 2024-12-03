using FhirCandle.Search;
using Hl7.Fhir.Model;

namespace FhirCandle.Smart
{
    public class R4CompartmentManager
    {
        private readonly SearchTester _searchTester;

        public R4CompartmentManager(CompartmentDefinition compartmentDefinition)
        {
            CompartmentDefinition = compartmentDefinition;
            _searchTester = new SearchTester
            {
                FhirStore = null
            };
        }

        public CompartmentDefinition CompartmentDefinition { get; }

        public bool isResourceTypeInCompartment(ResourceType patient)
        {
            var resourceComponent =  this.CompartmentDefinition.Resource
                .Find( resourceComponent => resourceComponent.Code == patient );
            return ( resourceComponent is { Param: not null } && resourceComponent.Param.Any() );
        }

        public bool isResourceInCompartment(Resource resource, Resource patient )
        {
            if ( resource.GetType() == patient.GetType() && resource.Id == patient.Id )
            {
                return true;
            }
            var resourceComponent =  this.CompartmentDefinition.Resource
                .Find( resourceComponent => resourceComponent.Code.ToString() == resource.TypeName );

            if (resourceComponent is { Param: not null } && resourceComponent.Param.Any())
            {
                return resourceComponent.Param.Where(param =>
                {
                    // _searchTester.TestForMatch( resource)
                    // param.ToString()=="" ).Any();
                    return false;
                }).Any();

            }
            return false;
        }
    }
}
