import { OrgUnitListView } from '../../components/organization/OrgUnitListView'
import { designationModule } from '../../components/organization/orgUnits'

export default function DesignationList() {
  return <OrgUnitListView module={designationModule} />
}
