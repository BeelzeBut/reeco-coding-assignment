import * as React from "react";
import { Check, Minus } from "lucide-react";
import { cn } from "@/lib/utils";

export interface CheckboxProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> {
  indeterminate?: boolean;
}

const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, checked, indeterminate, disabled, ...props }, forwardedRef) => {
    const innerRef = React.useRef<HTMLInputElement | null>(null);

    React.useImperativeHandle<HTMLInputElement | null, HTMLInputElement | null>(
      forwardedRef,
      () => innerRef.current
    );

    React.useEffect(() => {
      if (innerRef.current) innerRef.current.indeterminate = !!indeterminate;
    }, [indeterminate]);

    const isChecked = !!checked && !indeterminate;
    const ariaChecked: boolean | "mixed" = indeterminate ? "mixed" : isChecked;

    return (
      <span
        className={cn(
          "relative inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-sm border border-input bg-background shadow-sm transition-colors",
          "ring-offset-background focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2",
          (isChecked || indeterminate) && "border-primary bg-primary text-primary-foreground",
          disabled && "cursor-not-allowed opacity-50",
          className
        )}
      >
        <input
          type="checkbox"
          ref={innerRef}
          checked={isChecked}
          aria-checked={ariaChecked}
          disabled={disabled}
          className="absolute inset-0 cursor-pointer appearance-none opacity-0 disabled:cursor-not-allowed"
          {...props}
        />
        {indeterminate ? (
          <Minus className="h-3 w-3" strokeWidth={3} />
        ) : isChecked ? (
          <Check className="h-3 w-3" strokeWidth={3} />
        ) : null}
      </span>
    );
  }
);
Checkbox.displayName = "Checkbox";

export { Checkbox };
