import { OrgUnitCreate } from '../../components/organization/OrgUnitEditor'
import { designationModule } from '../../components/organization/orgUnits'

export default function DesignationCreate() {
  return <OrgUnitCreate module={designationModule} />
}
