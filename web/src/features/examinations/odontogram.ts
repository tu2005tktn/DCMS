export const surfaceOptions = [
  { value: 'O', label: 'Nhai / cắn' }, { value: 'B', label: 'Ngoài' },
  { value: 'L', label: 'Trong' }, { value: 'M', label: 'Gần' }, { value: 'D', label: 'Xa' },
];
export const surfaceText = (values?: string[] | null) => values?.map(v => surfaceOptions.find(x => x.value === v)?.label ?? v).join(', ');
