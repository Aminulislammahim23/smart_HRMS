import { OrgUnitCreate } from '../../components/organization/OrgUnitEditor'
import { departmentModule } from '../../components/organization/orgUnits'

export default function DepartmentCreate() {
  return <OrgUnitCreate module={departmentModule} />
}
